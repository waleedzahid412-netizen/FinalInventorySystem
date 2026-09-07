using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using InventorySystem.Data;
using InventorySystem.DTOs.Common;
using InventorySystem.DTOs.Purchases;
using InventorySystem.Helpers;
using InventorySystem.Models.Entities;
using InventorySystem.Repositories.Interfaces;
using InventorySystem.Services.Interfaces;

namespace InventorySystem.Services.Implementations
{
    public class PurchaseService : IPurchaseService
    {
        private readonly IPurchaseRepository _purchaseRepository;
        private readonly ApplicationDbContext _dbContext;
        private readonly IFifoCostingService _fifoCostingService;

        public PurchaseService(
            IPurchaseRepository purchaseRepository,
            ApplicationDbContext dbContext,
            IFifoCostingService fifoCostingService)
        {
            _purchaseRepository = purchaseRepository;
            _dbContext = dbContext;
            _fifoCostingService = fifoCostingService;
        }

        public async Task<OperationResult<int>> CreateAndFinalizePurchaseInvoiceAsync(CreatePurchaseInvoiceDto dto, int userId, CancellationToken cancellationToken = default)
        {
            // ===== 1. VALIDATE REQUEST =====
            if (dto == null)
            {
                return OperationResult<int>.Fail("Invalid purchase invoice request.");
            }

            if (dto.CompanyID <= 0)
            {
                return OperationResult<int>.Fail("Please select a valid Company.");
            }

            if (dto.WarehouseID <= 0)
            {
                return OperationResult<int>.Fail("Please select a valid Warehouse.");
            }

            if (dto.Items == null || dto.Items.Count == 0)
            {
                return OperationResult<int>.Fail("Purchase invoice must contain at least one line item.");
            }

            // ===== 2. VALIDATE ENTITIES =====
            if (!await _purchaseRepository.CompanyExistsAsync(dto.CompanyID, cancellationToken))
            {
                return OperationResult<int>.Fail("The selected Company does not exist or has been deleted.");
            }

            if (!await _purchaseRepository.WarehouseExistsAsync(dto.WarehouseID, cancellationToken))
            {
                return OperationResult<int>.Fail("The selected Warehouse does not exist or is inactive.");
            }

            // System invoice number is always auto-generated; manual field is SupplierInvoiceNumber.
            string? supplierInvoiceNumber = string.IsNullOrWhiteSpace(dto.SupplierInvoiceNumber)
                ? null
                : dto.SupplierInvoiceNumber.Trim();

            // Prepare item snapshots and unit conversion math using centralized helper
            decimal grandTotal = 0m;
            var itemSnapshots = new List<PurchaseInvoiceItem>();
            var itemTransactionList = new List<(PurchaseInvoiceItem Item, decimal ConvertedQty, decimal ConversionToBase)>();

            foreach (var itemDto in dto.Items)
            {
                if (itemDto.ProductID <= 0)
                {
                    return OperationResult<int>.Fail("Line item contains an invalid Product ID.");
                }

                if (itemDto.Quantity <= 0)
                {
                    return OperationResult<int>.Fail("Line item quantity must be greater than zero.");
                }

                if (itemDto.UnitCost < 0)
                {
                    return OperationResult<int>.Fail("Line item unit cost cannot be negative.");
                }

                if (!await _purchaseRepository.ProductExistsAsync(itemDto.ProductID, cancellationToken))
                {
                    return OperationResult<int>.Fail($"Product ID {itemDto.ProductID} does not exist or is inactive.");
                }

                var productUnit = await _purchaseRepository.GetProductUnitAsync(itemDto.ProductUnitID, cancellationToken);
                if (productUnit == null || productUnit.ProductID != itemDto.ProductID)
                {
                    return OperationResult<int>.Fail($"Selected unit is not valid for Product ID {itemDto.ProductID}.");
                }

                // ===== 3. CENTRALIZED UNIT CONVERSION =====
                decimal convertedQuantity = UnitConversionHelper.ToBaseUnits(itemDto.Quantity, productUnit.ConversionToBaseUnit);

                // ===== 4. CALCULATE TOTALS =====
                decimal totalCost = Math.Round(itemDto.Quantity * itemDto.UnitCost, 2, MidpointRounding.AwayFromZero);
                grandTotal += totalCost;

                var invoiceItem = new PurchaseInvoiceItem
                {
                    ProductID = itemDto.ProductID,
                    ProductUnitID = itemDto.ProductUnitID,
                    Quantity = itemDto.Quantity,
                    ConvertedQuantity = convertedQuantity,
                    UnitCost = itemDto.UnitCost,
                    TotalCost = totalCost,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = userId,
                    IsDeleted = false
                };

                itemSnapshots.Add(invoiceItem);
                itemTransactionList.Add((invoiceItem, convertedQuantity, productUnit.ConversionToBaseUnit));
            }

            const int maxInvoiceNumberAttempts = 5;
            for (int attempt = 1; attempt <= maxInvoiceNumberAttempts; attempt++)
            {
                string invoiceNumber = await _purchaseRepository.GenerateNextPurchaseInvoiceNumberAsync(cancellationToken);

                await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
                try
                {
                // ===== 5. CREATE PURCHASE INVOICE HEADER =====
                var invoice = new PurchaseInvoice
                {
                    CompanyID = dto.CompanyID,
                    WarehouseID = dto.WarehouseID,
                    InvoiceNumber = invoiceNumber,
                    SupplierInvoiceNumber = supplierInvoiceNumber,
                    InvoiceDate = dto.InvoiceDate == default ? DateTime.Today : dto.InvoiceDate,
                    SubTotal = grandTotal,
                    TaxAmount = 0m,
                    DiscountAmount = 0m,
                    GrandTotal = grandTotal,
                    PaidAmount = 0m,
                    PaymentStatus = "UNPAID",
                    CreatedBy = userId,
                    CreatedAt = DateTime.UtcNow,
                    IsDeleted = false
                };

                await _purchaseRepository.AddInvoiceAsync(invoice, cancellationToken);
                await _purchaseRepository.SaveChangesAsync(cancellationToken); // Generates PurchaseInvoiceID

                // ===== 6. CREATE PURCHASE INVOICE ITEMS =====
                foreach (var item in itemSnapshots)
                {
                    item.PurchaseInvoiceID = invoice.PurchaseInvoiceID;
                    await _purchaseRepository.AddInvoiceItemAsync(item, cancellationToken);
                }
                await _purchaseRepository.SaveChangesAsync(cancellationToken); // Generates PurchaseItemID for each item

                // ===== 7. BATCH INVENTORY STOCK UPDATES (Grouped by ProductID + WarehouseID) =====
                var stockGroups = itemTransactionList
                    .GroupBy(x => x.Item.ProductID)
                    .Select(g => new
                    {
                        ProductID = g.Key,
                        TotalConvertedQuantity = g.Sum(x => x.ConvertedQty)
                    });

                foreach (var group in stockGroups)
                {
                    var stock = await _purchaseRepository.GetInventoryStockTrackedAsync(group.ProductID, dto.WarehouseID, cancellationToken);
                    if (stock == null)
                    {
                        stock = new InventoryStock
                        {
                            ProductID = group.ProductID,
                            WarehouseID = dto.WarehouseID,
                            Quantity = group.TotalConvertedQuantity,
                            IsActive = true,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = userId,
                            IsDeleted = false
                        };
                        await _purchaseRepository.AddInventoryStockAsync(stock, cancellationToken);
                    }
                    else
                    {
                        stock.Quantity += group.TotalConvertedQuantity;
                        stock.UpdatedAt = DateTime.UtcNow;
                        stock.UpdatedBy = userId;
                    }
                }

                // ===== 8. CREATE INVENTORY TRANSACTIONS & FIFO COST LAYERS (One per line item) =====
                foreach (var tuple in itemTransactionList)
                {
                    var invTx = new InventoryTransaction
                    {
                        ProductID = tuple.Item.ProductID,
                        WarehouseID = dto.WarehouseID,
                        TransactionType = "PURCHASE",
                        Quantity = tuple.ConvertedQty, // Positive for stock inward
                        ReferenceNumber = invoiceNumber,
                        PurchaseInvoiceItemID = tuple.Item.PurchaseItemID,
                        CreatedBy = userId,
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true,
                        IsDeleted = false
                    };

                    await _purchaseRepository.AddInventoryTransactionAsync(invTx, cancellationToken);

                    await _fifoCostingService.CreateLayerFromPurchaseAsync(
                        tuple.Item,
                        dto.WarehouseID,
                        invoice.InvoiceDate,
                        tuple.ConversionToBase,
                        userId,
                        cancellationToken);
                }

                // ===== 9. CREATE COMPANY LEDGER ENTRY =====
                var ledgerEntry = new CompanyLedger
                {
                    CompanyID = dto.CompanyID,
                    TransactionDate = invoice.InvoiceDate,
                    TransactionType = "PURCHASE",
                    DebitAmount = 0m,
                    CreditAmount = grandTotal, // Credit increases payable liability to supplier
                    PurchaseInvoiceID = invoice.PurchaseInvoiceID,
                    Description = $"Purchase Invoice #{invoiceNumber}",
                    CreatedBy = userId,
                    CreatedAt = DateTime.UtcNow
                };

                await _purchaseRepository.AddCompanyLedgerAsync(ledgerEntry, cancellationToken);

                // Save all pending entities in single transaction session
                await _purchaseRepository.SaveChangesAsync(cancellationToken);

                // ===== 10. COMMIT TRANSACTION =====
                await transaction.CommitAsync(cancellationToken);

                return OperationResult<int>.Ok(invoice.PurchaseInvoiceID, "Purchase Invoice finalized successfully.");
                }
                catch (Exception ex) when (attempt < maxInvoiceNumberAttempts && DbExceptionHelper.IsUniqueConstraintViolation(ex))
                {
                    await transaction.RollbackAsync(cancellationToken);
                    _dbContext.ChangeTracker.Clear();
                    foreach (var item in itemSnapshots)
                    {
                        item.PurchaseItemID = 0;
                        item.PurchaseInvoiceID = 0;
                    }
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return OperationResult<int>.Fail(UserFacingErrorMessages.PurchaseFinalizeFailed);
                }
            }

            return OperationResult<int>.Fail("Could not assign a unique invoice number. Please try again.");
        }

        public async Task<PagedResult<PurchaseListDto>> GetPagedPurchasesAsync(PurchaseFilterDto filter, CancellationToken cancellationToken = default)
        {
            return await _purchaseRepository.GetPagedAsync(filter, cancellationToken);
        }

        public async Task<PurchaseDetailsDto?> GetPurchaseDetailsAsync(int purchaseInvoiceId, CancellationToken cancellationToken = default)
        {
            return await _purchaseRepository.GetDetailsByIdAsync(purchaseInvoiceId, cancellationToken);
        }

        public async Task<PagedResult<PurchasePaymentHistoryDto>> GetPaymentHistoryTabAsync(int purchaseInvoiceId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        {
            return await _purchaseRepository.GetPaymentHistoryAsync(purchaseInvoiceId, pageNumber, pageSize, cancellationToken);
        }

        public async Task<PagedResult<PurchaseLedgerEntryDto>> GetLedgerTabAsync(int purchaseInvoiceId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        {
            return await _purchaseRepository.GetLedgerByInvoiceAsync(purchaseInvoiceId, pageNumber, pageSize, cancellationToken);
        }

        public async Task<OperationResult> CanEditPurchaseInvoiceAsync(int purchaseInvoiceId, CancellationToken cancellationToken = default)
        {
            var invoice = await _dbContext.PurchaseInvoices
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.PurchaseInvoiceID == purchaseInvoiceId && !p.IsDeleted, cancellationToken);

            if (invoice == null)
            {
                return OperationResult.Fail($"Purchase Invoice #{purchaseInvoiceId} not found.");
            }

            if (invoice.PaidAmount > 0)
            {
                return OperationResult.Fail("This invoice cannot be edited because payment has already been recorded against it.");
            }

            bool hasPayments = await _dbContext.CompanyPayments
                .AsNoTracking()
                .AnyAsync(p => p.PurchaseInvoiceID == purchaseInvoiceId && !p.IsDeleted, cancellationToken);

            if (hasPayments)
            {
                return OperationResult.Fail("This invoice cannot be edited because payment records already exist against it.");
            }

            bool hasReturns = await _dbContext.PurchaseReturns
                .AsNoTracking()
                .AnyAsync(r => r.PurchaseInvoiceID == purchaseInvoiceId && !r.IsDeleted, cancellationToken);

            if (hasReturns)
            {
                return OperationResult.Fail("This invoice cannot be edited because return transactions have already been processed against it.");
            }

            return OperationResult.Ok("Invoice is eligible for edit.");
        }

        public async Task<OperationResult<int>> UpdatePurchaseInvoiceAsync(UpdatePurchaseInvoiceDto dto, int userId, CancellationToken cancellationToken = default)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            if (dto.PurchaseInvoiceID <= 0) return OperationResult<int>.Fail("Invalid purchase invoice ID.");

            if (string.IsNullOrWhiteSpace(dto.EditReason))
            {
                return OperationResult<int>.Fail("An edit reason is required to edit an invoice.");
            }

            var canEditCheck = await CanEditPurchaseInvoiceAsync(dto.PurchaseInvoiceID, cancellationToken);
            if (!canEditCheck.Success)
            {
                return OperationResult<int>.Fail(canEditCheck.Message);
            }

            var invoice = await _dbContext.PurchaseInvoices
                .Include(p => p.Company)
                .Include(p => p.Items.Where(i => !i.IsDeleted))
                .FirstOrDefaultAsync(p => p.PurchaseInvoiceID == dto.PurchaseInvoiceID && !p.IsDeleted, cancellationToken);

            if (invoice == null)
            {
                return OperationResult<int>.Fail($"Purchase Invoice #{dto.PurchaseInvoiceID} not found.");
            }

            if (dto.Items == null || !dto.Items.Any())
            {
                return OperationResult<int>.Fail("Purchase invoice must contain at least one line item.");
            }

            // Pre-process updated items
            var processedNewItems = new List<(CreatePurchaseItemDto Dto, ProductUnit Unit, decimal ConvertedQty, decimal LineTotal)>();
            decimal newSubTotal = 0m;

            foreach (var itemDto in dto.Items)
            {
                if (itemDto.ProductID <= 0 || itemDto.ProductUnitID <= 0 || itemDto.Quantity <= 0 || itemDto.UnitCost < 0)
                {
                    return OperationResult<int>.Fail("One or more line items have invalid product, unit, quantity, or unit cost.");
                }

                var productUnit = await _purchaseRepository.GetProductUnitAsync(itemDto.ProductUnitID, cancellationToken);
                if (productUnit == null || productUnit.ProductID != itemDto.ProductID)
                {
                    return OperationResult<int>.Fail($"Selected unit is not valid for product ID {itemDto.ProductID}.");
                }

                decimal convertedQty = UnitConversionHelper.ToBaseUnits(itemDto.Quantity, productUnit.ConversionToBaseUnit);
                decimal lineTotal = Math.Round(itemDto.Quantity * itemDto.UnitCost, 2);

                processedNewItems.Add((itemDto, productUnit, convertedQty, lineTotal));
                newSubTotal += lineTotal;
            }

            decimal oldSubTotal = invoice.SubTotal;
            decimal oldDiscountTotal = invoice.DiscountAmount;
            decimal oldGrandTotal = invoice.GrandTotal;
            int previousVersion = invoice.Version;
            int previousItemCount = invoice.Items.Count(i => !i.IsDeleted);

            decimal newGrandTotal = newSubTotal;
            var now = DateTime.UtcNow;
            var oldActiveItems = invoice.Items.Where(i => !i.IsDeleted).ToList();

            foreach (var oldItem in oldActiveItems)
            {
                var reversalCheck = await ValidatePurchaseLineReversalAsync(
                    oldItem, invoice.WarehouseID, cancellationToken);
                if (!reversalCheck.Success)
                {
                    return OperationResult<int>.Fail(reversalCheck.Message);
                }
            }

            using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                // Step 1: REVERSE PREVIOUS STOCK ADDITIONS & FIFO LAYERS
                foreach (var oldItem in oldActiveItems)
                {
                    var layerResult = await _fifoCostingService.ReversePurchaseLayerAsync(
                        oldItem.PurchaseItemID, oldItem.ConvertedQuantity, userId, cancellationToken);
                    if (!layerResult.Success)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return OperationResult<int>.Fail(layerResult.Message);
                    }

                    var stock = await _purchaseRepository.GetInventoryStockTrackedAsync(oldItem.ProductID, invoice.WarehouseID, cancellationToken);
                    if (stock == null || stock.Quantity < oldItem.ConvertedQuantity)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        var productName = await GetProductNameAsync(oldItem.ProductID, cancellationToken);
                        decimal availableStock = stock?.Quantity ?? 0m;
                        return OperationResult<int>.Fail(string.Format(
                            UserFacingErrorMessages.PurchaseEditInsufficientStock,
                            productName,
                            availableStock,
                            oldItem.ConvertedQuantity));
                    }

                    stock.Quantity -= oldItem.ConvertedQuantity;
                    stock.UpdatedAt = now;
                }

                // Step 2: APPLY NEW STOCK ADDITION
                foreach (var tuple in processedNewItems)
                {
                    var stock = await _purchaseRepository.GetInventoryStockTrackedAsync(tuple.Dto.ProductID, invoice.WarehouseID, cancellationToken);
                    if (stock == null)
                    {
                        stock = new InventoryStock
                        {
                            ProductID = tuple.Dto.ProductID,
                            WarehouseID = invoice.WarehouseID,
                            Quantity = tuple.ConvertedQty,
                            UpdatedAt = now
                        };
                        await _purchaseRepository.AddInventoryStockAsync(stock, cancellationToken);
                    }
                    else
                    {
                        stock.Quantity += tuple.ConvertedQty;
                        stock.UpdatedAt = now;
                    }
                }

                // Step 3: LOG REVERSAL INVENTORY TRANSACTIONS
                foreach (var oldItem in oldActiveItems)
                {
                    var reversalTx = new InventoryTransaction
                    {
                        ProductID = oldItem.ProductID,
                        WarehouseID = invoice.WarehouseID,
                        TransactionType = "REVERSAL_OUT",
                        Quantity = oldItem.ConvertedQuantity,
                        ReferenceNumber = $"REVERSAL-PUR-{invoice.InvoiceNumber}",
                        PurchaseInvoiceItemID = oldItem.PurchaseItemID,
                        CreatedBy = userId,
                        CreatedAt = now,
                        IsActive = true,
                        IsDeleted = false
                    };
                    await _purchaseRepository.AddInventoryTransactionAsync(reversalTx, cancellationToken);
                }

                // Step 4: SOFT DELETE OLD LINE ITEMS
                foreach (var oldItem in oldActiveItems)
                {
                    oldItem.IsDeleted = true;
                    oldItem.DeletedAt = now;
                }

                // Step 5: INSERT NEW ACTIVE LINE ITEMS & LOG INVENTORY TRANSACTIONS
                foreach (var tuple in processedNewItems)
                {
                    var newItem = new PurchaseInvoiceItem
                    {
                        PurchaseInvoiceID = invoice.PurchaseInvoiceID,
                        ProductID = tuple.Dto.ProductID,
                        ProductUnitID = tuple.Dto.ProductUnitID,
                        Quantity = tuple.Dto.Quantity,
                        ConvertedQuantity = tuple.ConvertedQty,
                        UnitCost = tuple.Dto.UnitCost,
                        TotalCost = tuple.LineTotal,
                        IsDeleted = false
                    };
                    await _dbContext.PurchaseInvoiceItems.AddAsync(newItem, cancellationToken);
                    await _dbContext.SaveChangesAsync(cancellationToken); // Generates PurchaseItemID

                    var newTx = new InventoryTransaction
                    {
                        ProductID = tuple.Dto.ProductID,
                        WarehouseID = invoice.WarehouseID,
                        TransactionType = "PURCHASE",
                        Quantity = tuple.ConvertedQty,
                        ReferenceNumber = invoice.InvoiceNumber,
                        PurchaseInvoiceItemID = newItem.PurchaseItemID,
                        CreatedBy = userId,
                        CreatedAt = now,
                        IsActive = true,
                        IsDeleted = false
                    };
                    await _purchaseRepository.AddInventoryTransactionAsync(newTx, cancellationToken);

                    await _fifoCostingService.CreateLayerFromPurchaseAsync(
                        newItem,
                        invoice.WarehouseID,
                        dto.InvoiceDate,
                        tuple.Unit.ConversionToBaseUnit,
                        userId,
                        cancellationToken);
                }

                // Step 6: UPDATE INVOICE HEADER & METADATA (IMMUTABLE FIELDS PRESERVED: CompanyID, WarehouseID, InvoiceNumber)
                invoice.InvoiceDate = dto.InvoiceDate;
                invoice.SubTotal = newSubTotal;
                invoice.GrandTotal = newGrandTotal;
                invoice.Version += 1;
                invoice.UpdatedAt = now;
                invoice.UpdatedBy = userId;

                // Step 7: APPEND-ONLY COMPANY LEDGER CORRECTION (BR-002 — never mutate original PURCHASE row)
                var ledgerCorrections = new List<CompanyLedger>();
                LedgerAppendHelper.AppendPurchaseInvoiceCorrection(
                    invoice, oldGrandTotal, newGrandTotal, dto.InvoiceDate, userId, now, ledgerCorrections);
                foreach (var entry in ledgerCorrections)
                {
                    await _dbContext.CompanyLedgers.AddAsync(entry, cancellationToken);
                }

                // Step 8: LOG EXPANDED IMMUTABLE INVOICE EDIT AUDIT RECORD
                var auditLog = new InvoiceEditAudit
                {
                    InvoiceID = invoice.PurchaseInvoiceID,
                    InvoiceType = "Purchase",
                    PreviousVersion = previousVersion,
                    NewVersion = invoice.Version,
                    PreviousItemCount = previousItemCount,
                    NewItemCount = processedNewItems.Count,
                    OldSubTotal = oldSubTotal,
                    NewSubTotal = newSubTotal,
                    OldDiscountTotal = oldDiscountTotal,
                    NewDiscountTotal = 0m,
                    OldGrandTotal = oldGrandTotal,
                    NewGrandTotal = newGrandTotal,
                    EditReason = dto.EditReason.Trim(),
                    EditedBy = userId,
                    EditedAt = now
                };
                await _dbContext.InvoiceEditAudits.AddAsync(auditLog, cancellationToken);

                await _purchaseRepository.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return OperationResult<int>.Ok(invoice.PurchaseInvoiceID, $"Purchase Invoice #{invoice.InvoiceNumber} updated to v{invoice.Version} successfully.");
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync(cancellationToken);
                return OperationResult<int>.Fail("This invoice has already been modified by another user. Please refresh and try again.");
            }
            catch (Exception)
            {
                await transaction.RollbackAsync(cancellationToken);
                return OperationResult<int>.Fail(UserFacingErrorMessages.PurchaseUpdateFailed);
            }
        }

        private async Task<OperationResult> ValidatePurchaseLineReversalAsync(
            PurchaseInvoiceItem oldItem,
            int warehouseId,
            CancellationToken cancellationToken)
        {
            var layer = await _dbContext.InventoryCostLayers
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    l => l.PurchaseInvoiceItemID == oldItem.PurchaseItemID && !l.IsDeleted,
                    cancellationToken);

            if (layer == null)
            {
                return OperationResult.Fail(UserFacingErrorMessages.PurchaseEditMissingCostLayer);
            }

            if (layer.RemainingQuantity < oldItem.ConvertedQuantity)
            {
                decimal soldBaseUnits = oldItem.ConvertedQuantity - layer.RemainingQuantity;
                return OperationResult.Fail(UserFacingErrorMessages.PurchaseEditBatchAlreadySold(soldBaseUnits));
            }

            var stock = await _purchaseRepository.GetInventoryStockTrackedAsync(
                oldItem.ProductID, warehouseId, cancellationToken);
            decimal availableStock = stock?.Quantity ?? 0m;

            if (availableStock < oldItem.ConvertedQuantity)
            {
                var productName = await GetProductNameAsync(oldItem.ProductID, cancellationToken);
                return OperationResult.Fail(string.Format(
                    UserFacingErrorMessages.PurchaseEditInsufficientStock,
                    productName,
                    availableStock,
                    oldItem.ConvertedQuantity));
            }

            return OperationResult.Ok();
        }

        private async Task<string> GetProductNameAsync(int productId, CancellationToken cancellationToken)
        {
            return await _dbContext.Products
                .AsNoTracking()
                .Where(p => p.ProductID == productId)
                .Select(p => p.ProductName)
                .FirstOrDefaultAsync(cancellationToken) ?? $"Product #{productId}";
        }
    }
}
