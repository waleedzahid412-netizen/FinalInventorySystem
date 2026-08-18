using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using InventorySystem.Data;
using InventorySystem.DTOs.Common;
using InventorySystem.DTOs.Sales;
using InventorySystem.Helpers;
using InventorySystem.Models.Entities;
using InventorySystem.Repositories.Interfaces;
using InventorySystem.Services.Interfaces;

namespace InventorySystem.Services.Implementations
{
    public class SalesService : ISalesService
    {
        private readonly ISalesRepository _salesRepository;
        private readonly ApplicationDbContext _context;
        private readonly IPromotionDiscountService _promotionDiscountService;
        private readonly ILogger<SalesService> _logger;

        public SalesService(
            ISalesRepository salesRepository,
            ApplicationDbContext context,
            IPromotionDiscountService promotionDiscountService,
            ILogger<SalesService> logger)
        {
            _salesRepository = salesRepository;
            _context = context;
            _promotionDiscountService = promotionDiscountService;
            _logger = logger;
        }

        public async Task<PagedResult<SalesListDto>> GetPagedSalesAsync(SalesFilterDto filter, CancellationToken cancellationToken = default)
        {
            return await _salesRepository.GetPagedAsync(filter, cancellationToken);
        }

        public async Task<SalesDetailsDto?> GetSalesDetailsAsync(int salesInvoiceId, CancellationToken cancellationToken = default)
        {
            return await _salesRepository.GetDetailsByIdAsync(salesInvoiceId, cancellationToken);
        }

        public async Task<PagedResult<SalesPaymentHistoryDto>> GetPaymentHistoryTabAsync(int salesInvoiceId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        {
            return await _salesRepository.GetPaymentHistoryAsync(salesInvoiceId, pageNumber, pageSize, cancellationToken);
        }

        public async Task<PagedResult<SalesLedgerEntryDto>> GetLedgerTabAsync(int salesInvoiceId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        {
            return await _salesRepository.GetLedgerByInvoiceAsync(salesInvoiceId, pageNumber, pageSize, cancellationToken);
        }

        public async Task<OperationResult<int>> CreateAndFinalizeSalesInvoiceAsync(CreateSalesInvoiceDto dto, int userId, CancellationToken cancellationToken = default)
        {
            // ===== 1. BASIC VALIDATIONS =====
            if (dto == null)
            {
                return OperationResult<int>.Fail("Invalid sales invoice data.");
            }

            if (dto.CustomerID <= 0)
            {
                return OperationResult<int>.Fail("Please select a valid customer.");
            }

            if (dto.WarehouseID <= 0)
            {
                return OperationResult<int>.Fail("Please select a destination warehouse.");
            }

            if (dto.Items == null || !dto.Items.Any())
            {
                return OperationResult<int>.Fail("A sales invoice must contain at least one line item.");
            }

            // Validate Customer
            var customer = await _salesRepository.GetCustomerForInvoiceAsync(dto.CustomerID, cancellationToken);
            if (customer == null)
            {
                return OperationResult<int>.Fail("Selected customer does not exist or is inactive.");
            }

            // Validate Warehouse
            if (!await _salesRepository.WarehouseExistsAsync(dto.WarehouseID, cancellationToken))
            {
                return OperationResult<int>.Fail("Selected warehouse does not exist or is inactive.");
            }

            // Validate DeliveryPerson if assigned
            if (dto.DeliveryPersonID.HasValue && dto.DeliveryPersonID.Value > 0)
            {
                if (!await _salesRepository.DeliveryPersonExistsAsync(dto.DeliveryPersonID.Value, cancellationToken))
                {
                    return OperationResult<int>.Fail("Selected delivery person does not exist or is inactive.");
                }
            }

            var headerValidation = await ValidateInvoiceHeaderAsync(dto.BrokerID, dto.SalespersonID, cancellationToken);
            if (!headerValidation.Success)
            {
                return OperationResult<int>.Fail(headerValidation.Message);
            }

            // Auto-generate Invoice Number if blank or default text
            string invoiceNumber = dto.InvoiceNumber?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(invoiceNumber) || string.Equals(invoiceNumber, "Auto-generated on save", StringComparison.OrdinalIgnoreCase))
            {
                invoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";
            }

            if (await _salesRepository.ExistsByInvoiceNumberAsync(invoiceNumber, null, cancellationToken))
            {
                invoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";
            }

            // Ignore client FREE lines — only NORMAL cart items are trusted (same approach as UpdateSalesInvoiceAsync)
            var normalInputItems = dto.Items
                .Where(i => !string.Equals(i.ItemType, "FREE", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!normalInputItems.Any())
            {
                return OperationResult<int>.Fail("Sales invoice must contain at least one paid product item.");
            }

            // Build cart for authoritative promotion evaluation from NORMAL items only
            var cartItems = new List<CartItemDto>();
            decimal rawPaidSubTotal = 0m;

            foreach (var itemDto in normalInputItems)
            {
                if (itemDto.ProductID <= 0)
                {
                    return OperationResult<int>.Fail("One or more line items have an invalid product.");
                }

                if (itemDto.ProductUnitID <= 0)
                {
                    return OperationResult<int>.Fail("One or more line items have an invalid unit.");
                }

                if (itemDto.Quantity <= 0)
                {
                    return OperationResult<int>.Fail("Item quantity must be greater than zero.");
                }

                if (itemDto.UnitPrice < 0)
                {
                    return OperationResult<int>.Fail("Unit price cannot be negative.");
                }

                var productUnitCheck = await _salesRepository.GetProductUnitAsync(itemDto.ProductUnitID, cancellationToken);
                if (productUnitCheck == null || productUnitCheck.ProductID != itemDto.ProductID)
                {
                    return OperationResult<int>.Fail($"Selected unit is not valid for product ID {itemDto.ProductID}.");
                }

                rawPaidSubTotal += (itemDto.Quantity * itemDto.UnitPrice);
                cartItems.Add(new CartItemDto
                {
                    ProductID = itemDto.ProductID,
                    ProductUnitID = itemDto.ProductUnitID,
                    Quantity = itemDto.Quantity,
                    UnitPrice = itemDto.UnitPrice,
                    ItemType = "NORMAL"
                });
            }

            var orderContext = new OrderContextDto
            {
                CustomerID = dto.CustomerID,
                WarehouseID = dto.WarehouseID,
                SubTotal = rawPaidSubTotal,
                Items = cartItems
            };

            // Rebuild authoritative line items (paid NORMAL; FREE only when seller opts in).
            // Client line DiscountAmount is never trusted — server allocates invoice-level discount.
            var authoritativeItems = new List<CreateSalesItemDto>();
            foreach (var item in normalInputItems)
            {
                authoritativeItems.Add(new CreateSalesItemDto
                {
                    ProductID = item.ProductID,
                    ProductUnitID = item.ProductUnitID,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    DiscountAmount = 0m,
                    DiscountRate = 0m,
                    ItemType = "NORMAL",
                    PromotionID = null
                });
            }

            // Authoritative promotion evaluation only when ApplyPromotions is true
            if (dto.ApplyPromotions)
            {
                var evalResult = await _promotionDiscountService.EvaluatePromotionsAndDiscountsAsync(orderContext, cancellationToken);

                if (evalResult != null && evalResult.Promotions != null)
                {
                    foreach (var promo in evalResult.Promotions)
                    {
                        AppendAuthoritativeFreeItem(authoritativeItems, promo);
                    }
                }
            }

            // Pre-process authoritative items and calculate base unit conversions & totals
            var processedItems = new List<(CreateSalesItemDto Dto, ProductUnit? Unit, decimal ConvertedQty, decimal LineSubTotal)>();
            decimal invoiceSubTotal = 0m;

            // Group required quantities per product (includes NORMAL and authoritative FREE product items; excludes custom FREE)
            var productRequiredBaseQty = new Dictionary<int, (string ProductName, decimal RequiredQty)>();

            foreach (var itemDto in authoritativeItems)
            {
                bool isCustomFree = string.Equals(itemDto.ItemType, "FREE", StringComparison.OrdinalIgnoreCase) && itemDto.IsCustomFreeItem;
                if (isCustomFree)
                {
                    processedItems.Add((itemDto, null, itemDto.Quantity, 0m));
                    continue;
                }

                var productUnit = await _salesRepository.GetProductUnitAsync(itemDto.ProductUnitID, cancellationToken);
                if (productUnit == null || productUnit.ProductID != itemDto.ProductID)
                {
                    return OperationResult<int>.Fail($"Selected unit is not valid for product ID {itemDto.ProductID}.");
                }

                var productCheck = await ValidateProductIsSellableAsync(itemDto.ProductID, cancellationToken);
                if (!productCheck.Success)
                {
                    return OperationResult<int>.Fail(productCheck.Message);
                }

                // Centralized conversion helper
                decimal convertedQty = UnitConversionHelper.ToBaseUnits(itemDto.Quantity, productUnit.ConversionToBaseUnit);
                decimal effectiveUnitPrice = string.Equals(itemDto.ItemType, "FREE", StringComparison.OrdinalIgnoreCase) ? 0m : itemDto.UnitPrice;
                decimal lineSubTotal = itemDto.Quantity * effectiveUnitPrice;

                processedItems.Add((itemDto, productUnit, convertedQty, lineSubTotal));

                invoiceSubTotal += lineSubTotal;

                // Accumulate stock requirements (both NORMAL and FREE product items require inventory stock deduction)
                if (!productRequiredBaseQty.ContainsKey(itemDto.ProductID))
                {
                    productRequiredBaseQty[itemDto.ProductID] = (productUnit.Product.ProductName, 0m);
                }
                var currentReq = productRequiredBaseQty[itemDto.ProductID];
                productRequiredBaseQty[itemDto.ProductID] = (currentReq.ProductName, currentReq.RequiredQty + convertedQty);
            }

            // ===== RESOLVE DISCOUNT MODE + ALLOCATE ONTO NORMAL ITEMS =====
            var discountResolution = await ResolveInvoiceDiscountAsync(
                dto.DiscountMode,
                dto.AppliedDiscountRuleID,
                dto.ManualDiscountType,
                dto.ManualDiscountValue,
                invoiceSubTotal,
                autoPickFirstEligibleRule: false,
                cancellationToken);

            if (!discountResolution.Success)
            {
                return OperationResult<int>.Fail(discountResolution.ErrorMessage!);
            }

            ApplyDiscountAllocationToItems(authoritativeItems, discountResolution.HeaderDiscountAmount, discountResolution.DiscountType, discountResolution.DiscountValue);

            decimal invoiceDiscountTotal = discountResolution.HeaderDiscountAmount;
            decimal invoiceGrandTotal = Math.Max(0m, invoiceSubTotal - invoiceDiscountTotal);

            // ===== ATOMIC TRANSACTION EXECUTION =====
            using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var now = DateTime.UtcNow;

                // Step 1: STOCK AVAILABILITY CHECK & INVENTORY DEDUCTION (BR-007, BR-009)
                foreach (var kvp in productRequiredBaseQty)
                {
                    int productId = kvp.Key;
                    var (productName, requiredBaseQty) = kvp.Value;

                    var stock = await _salesRepository.GetInventoryStockTrackedAsync(productId, dto.WarehouseID, cancellationToken);
                    decimal currentStockQty = stock?.Quantity ?? 0m;

                    if (currentStockQty < requiredBaseQty)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return OperationResult<int>.Fail(
                            $"Insufficient stock for product '{productName}'. Available: {currentStockQty:N2}, Requested: {requiredBaseQty:N2}.");
                    }

                    // Deduct stock
                    stock!.Quantity -= requiredBaseQty;
                    stock.UpdatedAt = now;
                }

                // Step 2: INSERT SalesInvoice Header
                var invoice = new SalesInvoice
                {
                    CustomerID = dto.CustomerID,
                    BrokerID = dto.BrokerID,
                    SalespersonID = dto.SalespersonID,
                    WarehouseID = dto.WarehouseID,
                    DeliveryPersonID = (dto.DeliveryPersonID.HasValue && dto.DeliveryPersonID.Value > 0) ? dto.DeliveryPersonID.Value : null,
                    AreaID = customer.AreaID,       // Area snapshot from customer
                    SubAreaID = customer.SubAreaID,  // SubArea snapshot from customer
                    CreatedBy = userId,
                    InvoiceNumber = invoiceNumber,
                    InvoiceDate = dto.InvoiceDate,
                    SubTotal = invoiceSubTotal,
                    DiscountTotal = invoiceDiscountTotal,
                    DiscountMode = discountResolution.DiscountMode,
                    TaxTotal = 0m,
                    GrandTotal = invoiceGrandTotal,
                    PaidAmount = 0m,
                    PaymentStatus = "UNPAID",
                    IsLocked = false,
                    CreatedAt = now,
                    IsDeleted = false
                };

                await _salesRepository.AddInvoiceAsync(invoice, cancellationToken);
                await _salesRepository.SaveChangesAsync(cancellationToken); // Generates InvoiceID

                // Step 3: INSERT SalesInvoiceItems, InventoryTransactions, & InvoicePromotions Snapshots
                var appliedPromotionIds = new HashSet<int>();

                foreach (var pItem in processedItems)
                {
                    bool isFree = string.Equals(pItem.Dto.ItemType, "FREE", StringComparison.OrdinalIgnoreCase);
                    bool isCustomFree = isFree && pItem.Dto.IsCustomFreeItem;
                    decimal itemUnitPrice = isFree ? 0m : pItem.Dto.UnitPrice;

                    var invoiceItem = new SalesInvoiceItem
                    {
                        InvoiceID = invoice.InvoiceID,
                        ProductID = isCustomFree ? null : pItem.Dto.ProductID,
                        ProductUnitID = isCustomFree ? null : pItem.Dto.ProductUnitID,
                        CustomItemName = isCustomFree ? pItem.Dto.CustomFreeItemName : null,
                        Quantity = pItem.Dto.Quantity,
                        ConvertedQuantity = pItem.ConvertedQty,
                        UnitPrice = itemUnitPrice,
                        DiscountRate = isFree ? 0m : pItem.Dto.DiscountRate,
                        DiscountAmount = isFree ? 0m : pItem.Dto.DiscountAmount,
                        ItemType = isFree ? "FREE" : "NORMAL",
                        PromotionID = pItem.Dto.PromotionID,
                        IsActive = true,
                        CreatedAt = now,
                        CreatedBy = userId,
                        IsDeleted = false
                    };

                    await _salesRepository.AddInvoiceItemAsync(invoiceItem, cancellationToken);
                    await _salesRepository.SaveChangesAsync(cancellationToken); // Generates InvoiceItemID

                    // Record Promotion Snapshot in InvoicePromotions (BR-028)
                    if (isFree && pItem.Dto.PromotionID.HasValue && pItem.Dto.PromotionID.Value > 0 && !appliedPromotionIds.Contains(pItem.Dto.PromotionID.Value))
                    {
                        appliedPromotionIds.Add(pItem.Dto.PromotionID.Value);
                        var promoSnapshot = new InvoicePromotion
                        {
                            InvoiceID = invoice.InvoiceID,
                            PromotionID = pItem.Dto.PromotionID.Value,
                            DiscountAmount = 0m,
                            AppliedBy = userId
                        };
                        await _context.InvoicePromotions.AddAsync(promoSnapshot, cancellationToken);
                    }

                    // Custom FREE gifts are non-inventory — no stock / transaction
                    if (isCustomFree)
                    {
                        continue;
                    }

                    // InventoryTransaction: Outward stock movement (Negative Quantity)
                    var invTransaction = new InventoryTransaction
                    {
                        ProductID = pItem.Dto.ProductID,
                        WarehouseID = dto.WarehouseID,
                        TransactionType = "SALE",
                        Quantity = -pItem.ConvertedQty, // Negative for outward sale
                        ReferenceNumber = invoiceNumber,
                        SalesInvoiceItemID = invoiceItem.InvoiceItemID,
                        CreatedAt = now,
                        CreatedBy = userId
                    };

                    await _salesRepository.AddInventoryTransactionAsync(invTransaction, cancellationToken);
                }

                // Step 4: INSERT Order Discount Snapshot in InvoiceDiscounts (BR-031)
                if (discountResolution.HeaderDiscountAmount > 0)
                {
                    var discountSnapshot = new InvoiceDiscount
                    {
                        InvoiceID = invoice.InvoiceID,
                        DiscountRuleID = discountResolution.DiscountRuleID,
                        RuleName = discountResolution.RuleName,
                        DiscountType = discountResolution.DiscountType,
                        DiscountValue = discountResolution.DiscountValue,
                        DiscountAmount = discountResolution.HeaderDiscountAmount,
                        MinimumOrderAmount = discountResolution.MinimumOrderAmount,
                        MaximumOrderAmount = discountResolution.MaximumOrderAmount,
                        DiscountSource = discountResolution.DiscountSource,
                        AppliedBy = userId
                    };
                    await _context.InvoiceDiscounts.AddAsync(discountSnapshot, cancellationToken);
                }

                // Step 5: INSERT CustomerLedger (Debit Entry = Post-Discount GrandTotal) (BR-041)
                var ledgerEntry = new CustomerLedger
                {
                    CustomerID = dto.CustomerID,
                    TransactionDate = dto.InvoiceDate,
                    TransactionType = "SALE",
                    DebitAmount = invoiceGrandTotal,  // Post-discount GrandTotal
                    CreditAmount = 0m,
                    SalesInvoiceID = invoice.InvoiceID,
                    Description = $"Sales Invoice #{invoice.InvoiceNumber}",
                    CreatedBy = userId,
                    CreatedAt = now
                };

                await _salesRepository.AddCustomerLedgerAsync(ledgerEntry, cancellationToken);

                // Save all changes & commit transaction
                await _salesRepository.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                _logger.LogInformation("Sales Invoice #{InvoiceNumber} finalized successfully with ID {InvoiceID}", invoice.InvoiceNumber, invoice.InvoiceID);
                return OperationResult<int>.Ok(invoice.InvoiceID, "Sales Invoice finalized successfully.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "Failed to finalize Sales Invoice #{InvoiceNumber}", invoiceNumber);
                return OperationResult<int>.Fail($"Failed to finalize sales invoice: {ex.Message}");
            }
        }

        public async Task<OperationResult> CanEditSalesInvoiceAsync(int salesInvoiceId, CancellationToken cancellationToken = default)
        {
            var invoice = await _context.SalesInvoices
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.InvoiceID == salesInvoiceId && !s.IsDeleted, cancellationToken);

            if (invoice == null)
            {
                return OperationResult.Fail($"Sales Invoice #{salesInvoiceId} not found.");
            }

            if (invoice.PaidAmount > 0)
            {
                return OperationResult.Fail("This invoice cannot be edited because payment has already been recorded against it.");
            }

            bool hasPayments = await _context.CustomerPayments
                .AsNoTracking()
                .AnyAsync(p => p.InvoiceID == salesInvoiceId && !p.IsDeleted, cancellationToken);

            if (hasPayments)
            {
                return OperationResult.Fail("This invoice cannot be edited because payment records already exist against it.");
            }

            bool hasReturns = await _context.SalesReturns
                .AsNoTracking()
                .AnyAsync(r => r.InvoiceID == salesInvoiceId && !r.IsDeleted, cancellationToken);

            if (hasReturns)
            {
                return OperationResult.Fail("This invoice cannot be edited because return transactions have already been processed against it.");
            }

            return OperationResult.Ok("Invoice is eligible for edit.");
        }

        public async Task<OperationResult<int>> UpdateSalesInvoiceAsync(UpdateSalesInvoiceDto dto, int userId, CancellationToken cancellationToken = default)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            if (dto.InvoiceID <= 0) return OperationResult<int>.Fail("Invalid invoice ID.");

            if (string.IsNullOrWhiteSpace(dto.EditReason))
            {
                return OperationResult<int>.Fail("An edit reason is required to edit an invoice.");
            }

            var canEditCheck = await CanEditSalesInvoiceAsync(dto.InvoiceID, cancellationToken);
            if (!canEditCheck.Success)
            {
                return OperationResult<int>.Fail(canEditCheck.Message);
            }

            var invoice = await _context.SalesInvoices
                .Include(s => s.Customer)
                .Include(s => s.Items.Where(i => !i.IsDeleted))
                .FirstOrDefaultAsync(s => s.InvoiceID == dto.InvoiceID && !s.IsDeleted, cancellationToken);

            if (invoice == null)
            {
                return OperationResult<int>.Fail($"Sales Invoice #{dto.InvoiceID} not found.");
            }

            if (dto.Items == null || !dto.Items.Any())
            {
                return OperationResult<int>.Fail("Sales invoice must contain at least one line item.");
            }

            var headerValidation = await ValidateInvoiceHeaderAsync(dto.BrokerID, dto.SalespersonID, cancellationToken);
            if (!headerValidation.Success)
            {
                return OperationResult<int>.Fail(headerValidation.Message);
            }

            // 1. Separate paid (NORMAL) items from free promotional items
            var normalInputItems = dto.Items
                .Where(i => !string.Equals(i.ItemType, "FREE", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!normalInputItems.Any())
            {
                return OperationResult<int>.Fail("Sales invoice must contain at least one paid product item.");
            }

            // 2. Build CartItemDto order context for promotion & discount evaluation engine
            var cartItems = new List<CartItemDto>();
            decimal rawPaidSubTotal = 0m;

            foreach (var itemDto in normalInputItems)
            {
                if (itemDto.ProductID <= 0 || itemDto.ProductUnitID <= 0 || itemDto.Quantity <= 0 || itemDto.UnitPrice < 0)
                {
                    return OperationResult<int>.Fail("One or more line items have invalid product, unit, quantity, or price.");
                }

                rawPaidSubTotal += (itemDto.Quantity * itemDto.UnitPrice);
                cartItems.Add(new CartItemDto
                {
                    ProductID = itemDto.ProductID,
                    ProductUnitID = itemDto.ProductUnitID,
                    Quantity = itemDto.Quantity,
                    UnitPrice = itemDto.UnitPrice,
                    ItemType = "NORMAL"
                });
            }

            var orderContext = new OrderContextDto
            {
                CustomerID = invoice.CustomerID,
                WarehouseID = invoice.WarehouseID,
                SubTotal = rawPaidSubTotal,
                Items = cartItems
            };

            // 3. Rebuild authoritative line items (Paid items; FREE only when seller opts in).
            // Client line DiscountAmount is never trusted — server allocates invoice-level discount.
            EvaluationResultDto? evalResult = null;
            var authoritativeItems = new List<CreateSalesItemDto>();
            foreach (var item in normalInputItems)
            {
                authoritativeItems.Add(new CreateSalesItemDto
                {
                    ProductID = item.ProductID,
                    ProductUnitID = item.ProductUnitID,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    DiscountAmount = 0m,
                    DiscountRate = 0m,
                    ItemType = "NORMAL",
                    PromotionID = null
                });
            }

            if (dto.ApplyPromotions)
            {
                // Authoritative server-side promotion & discount evaluation
                evalResult = await _promotionDiscountService.EvaluatePromotionsAndDiscountsAsync(orderContext, cancellationToken);

                if (evalResult != null && evalResult.Promotions != null)
                {
                    foreach (var promo in evalResult.Promotions)
                    {
                        AppendAuthoritativeFreeItem(authoritativeItems, promo);
                    }
                }
            }

            // 4. Pre-process all authoritative line items & calculate conversions
            var processedNewItems = new List<(CreateSalesItemDto Dto, ProductUnit? Unit, decimal ConvertedQty, decimal LineSubTotal)>();
            decimal newSubTotal = 0m;
            var newProductRequiredBaseQty = new Dictionary<int, (string ProductName, decimal RequiredQty)>();

            foreach (var itemDto in authoritativeItems)
            {
                bool isCustomFree = string.Equals(itemDto.ItemType, "FREE", StringComparison.OrdinalIgnoreCase) && itemDto.IsCustomFreeItem;
                if (isCustomFree)
                {
                    processedNewItems.Add((itemDto, null, itemDto.Quantity, 0m));
                    continue;
                }

                var productUnit = await _salesRepository.GetProductUnitAsync(itemDto.ProductUnitID, cancellationToken);
                if (productUnit == null || productUnit.ProductID != itemDto.ProductID)
                {
                    return OperationResult<int>.Fail($"Selected unit is not valid for product ID {itemDto.ProductID}.");
                }

                var productCheck = await ValidateProductIsSellableAsync(itemDto.ProductID, cancellationToken);
                if (!productCheck.Success)
                {
                    return OperationResult<int>.Fail(productCheck.Message);
                }

                decimal convertedQty = UnitConversionHelper.ToBaseUnits(itemDto.Quantity, productUnit.ConversionToBaseUnit);
                decimal effectiveUnitPrice = string.Equals(itemDto.ItemType, "FREE", StringComparison.OrdinalIgnoreCase) ? 0m : itemDto.UnitPrice;
                decimal lineSubTotal = itemDto.Quantity * effectiveUnitPrice;

                processedNewItems.Add((itemDto, productUnit, convertedQty, lineSubTotal));
                newSubTotal += lineSubTotal;

                if (!newProductRequiredBaseQty.ContainsKey(itemDto.ProductID))
                {
                    newProductRequiredBaseQty[itemDto.ProductID] = (productUnit.Product.ProductName, 0m);
                }
                var curr = newProductRequiredBaseQty[itemDto.ProductID];
                newProductRequiredBaseQty[itemDto.ProductID] = (curr.ProductName, curr.RequiredQty + convertedQty);
            }

            // 5. Resolve discount mode + allocate onto NORMAL items (same model as Create)
            int? autoPickRuleId = null;
            if ((!dto.AppliedDiscountRuleID.HasValue || dto.AppliedDiscountRuleID.Value <= 0)
                && string.Equals(NormalizeDiscountMode(dto.DiscountMode, dto.AppliedDiscountRuleID), "Automatic", StringComparison.OrdinalIgnoreCase))
            {
                autoPickRuleId = evalResult?.Discounts?.FirstOrDefault()?.DiscountRuleID;
            }

            var discountResolution = await ResolveInvoiceDiscountAsync(
                dto.DiscountMode,
                dto.AppliedDiscountRuleID ?? autoPickRuleId,
                dto.ManualDiscountType,
                dto.ManualDiscountValue,
                newSubTotal,
                autoPickFirstEligibleRule: false,
                cancellationToken);

            if (!discountResolution.Success)
            {
                return OperationResult<int>.Fail(discountResolution.ErrorMessage!);
            }

            ApplyDiscountAllocationToItems(authoritativeItems, discountResolution.HeaderDiscountAmount, discountResolution.DiscountType, discountResolution.DiscountValue);

            decimal newDiscountTotal = discountResolution.HeaderDiscountAmount;

            decimal oldSubTotal = invoice.SubTotal;
            decimal oldDiscountTotal = invoice.DiscountTotal;
            decimal oldGrandTotal = invoice.GrandTotal;
            int previousVersion = invoice.Version;
            int previousItemCount = invoice.Items.Count(i => !i.IsDeleted);

            decimal newGrandTotal = Math.Max(0m, newSubTotal - newDiscountTotal);
            var now = DateTime.UtcNow;

            using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                // Step 1: REVERSE PREVIOUS STOCK DEDUCTIONS
                var oldActiveItems = invoice.Items.Where(i => !i.IsDeleted).ToList();
                foreach (var oldItem in oldActiveItems)
                {
                    if (!oldItem.ProductID.HasValue)
                    {
                        continue; // custom FREE — no stock to restore
                    }

                    var stock = await _salesRepository.GetInventoryStockTrackedAsync(oldItem.ProductID.Value, invoice.WarehouseID, cancellationToken);
                    if (stock != null)
                    {
                        stock.Quantity += oldItem.ConvertedQuantity;
                        stock.UpdatedAt = now;
                    }
                }

                // Step 2: VALIDATE STOCK AVAILABILITY USING RESTORED STOCK
                foreach (var kvp in newProductRequiredBaseQty)
                {
                    int productId = kvp.Key;
                    var (productName, requiredBaseQty) = kvp.Value;

                    var stock = await _salesRepository.GetInventoryStockTrackedAsync(productId, invoice.WarehouseID, cancellationToken);
                    decimal availableStock = stock?.Quantity ?? 0m;

                    if (availableStock < requiredBaseQty)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return OperationResult<int>.Fail($"Insufficient stock for product '{productName}'. Available after restoration: {availableStock:N2}, Requested: {requiredBaseQty:N2}.");
                    }

                    // Apply new stock deduction
                    stock!.Quantity -= requiredBaseQty;
                    stock.UpdatedAt = now;
                }

                // Step 3: LOG REVERSAL INVENTORY TRANSACTIONS
                foreach (var oldItem in oldActiveItems)
                {
                    if (!oldItem.ProductID.HasValue)
                    {
                        continue;
                    }

                    var reversalTx = new InventoryTransaction
                    {
                        ProductID = oldItem.ProductID.Value,
                        WarehouseID = invoice.WarehouseID,
                        TransactionType = "REVERSAL_IN",
                        Quantity = oldItem.ConvertedQuantity,
                        ReferenceNumber = $"REVERSAL-INV-{invoice.InvoiceNumber}",
                        SalesInvoiceItemID = oldItem.InvoiceItemID,
                        CreatedBy = userId,
                        CreatedAt = now,
                        IsActive = true
                    };
                    await _salesRepository.AddInventoryTransactionAsync(reversalTx, cancellationToken);
                }

                // Step 4: SOFT DELETE OLD LINE ITEMS
                foreach (var oldItem in oldActiveItems)
                {
                    oldItem.IsDeleted = true;
                    oldItem.DeletedAt = now;
                }

                // Step 5: INSERT NEW ACTIVE LINE ITEMS & LOG INVENTORY TRANSACTIONS
                foreach (var itemTuple in processedNewItems)
                {
                    bool isFree = string.Equals(itemTuple.Dto.ItemType, "FREE", StringComparison.OrdinalIgnoreCase);
                    bool isCustomFree = isFree && itemTuple.Dto.IsCustomFreeItem;

                    var newInvoiceItem = new SalesInvoiceItem
                    {
                        InvoiceID = invoice.InvoiceID,
                        ProductID = isCustomFree ? null : itemTuple.Dto.ProductID,
                        ProductUnitID = isCustomFree ? null : itemTuple.Dto.ProductUnitID,
                        CustomItemName = isCustomFree ? itemTuple.Dto.CustomFreeItemName : null,
                        Quantity = itemTuple.Dto.Quantity,
                        ConvertedQuantity = itemTuple.ConvertedQty,
                        UnitPrice = isFree ? 0m : itemTuple.Dto.UnitPrice,
                        DiscountRate = isFree ? 0m : itemTuple.Dto.DiscountRate,
                        DiscountAmount = isFree ? 0m : itemTuple.Dto.DiscountAmount,
                        ItemType = itemTuple.Dto.ItemType,
                        PromotionID = itemTuple.Dto.PromotionID,
                        IsDeleted = false
                    };
                    await _context.SalesInvoiceItems.AddAsync(newInvoiceItem, cancellationToken);
                    await _context.SaveChangesAsync(cancellationToken);

                    if (isCustomFree)
                    {
                        continue;
                    }

                    var newTx = new InventoryTransaction
                    {
                        ProductID = itemTuple.Dto.ProductID,
                        WarehouseID = invoice.WarehouseID,
                        TransactionType = "SALE",
                        Quantity = -itemTuple.ConvertedQty,
                        ReferenceNumber = invoice.InvoiceNumber,
                        SalesInvoiceItemID = newInvoiceItem.InvoiceItemID,
                        CreatedBy = userId,
                        CreatedAt = now,
                        IsActive = true
                    };
                    await _salesRepository.AddInventoryTransactionAsync(newTx, cancellationToken);
                }

                // Step 6: UPDATE INVOICE HEADER & METADATA
                invoice.InvoiceDate = dto.InvoiceDate;
                invoice.BrokerID = dto.BrokerID;
                invoice.SalespersonID = dto.SalespersonID;
                invoice.DeliveryPersonID = (dto.DeliveryPersonID.HasValue && dto.DeliveryPersonID.Value > 0) ? dto.DeliveryPersonID.Value : null;
                invoice.SubTotal = newSubTotal;
                invoice.DiscountTotal = newDiscountTotal;
                invoice.DiscountMode = discountResolution.DiscountMode;
                invoice.GrandTotal = newGrandTotal;
                invoice.Version += 1;
                invoice.UpdatedAt = now;
                invoice.UpdatedBy = userId;

                // Sync InvoiceDiscounts to the current snapshot only (BR-031).
                // InvoiceEditAudit already records OldDiscountTotal/NewDiscountTotal for edit history.
                var oldInvoiceDiscounts = await _context.InvoiceDiscounts
                    .Where(d => d.InvoiceID == invoice.InvoiceID)
                    .ToListAsync(cancellationToken);
                if (oldInvoiceDiscounts.Any())
                {
                    _context.InvoiceDiscounts.RemoveRange(oldInvoiceDiscounts);
                }

                if (discountResolution.HeaderDiscountAmount > 0)
                {
                    _context.InvoiceDiscounts.Add(new InvoiceDiscount
                    {
                        InvoiceID = invoice.InvoiceID,
                        DiscountRuleID = discountResolution.DiscountRuleID,
                        RuleName = discountResolution.RuleName,
                        DiscountType = discountResolution.DiscountType,
                        DiscountValue = discountResolution.DiscountValue,
                        DiscountAmount = discountResolution.HeaderDiscountAmount,
                        MinimumOrderAmount = discountResolution.MinimumOrderAmount,
                        MaximumOrderAmount = discountResolution.MaximumOrderAmount,
                        DiscountSource = discountResolution.DiscountSource,
                        AppliedBy = userId
                    });
                }

                // Step 7: UPDATE CUSTOMER LEDGER
                var ledgerEntry = await _context.CustomerLedgers
                    .FirstOrDefaultAsync(l => l.SalesInvoiceID == invoice.InvoiceID && l.TransactionType == "SALE", cancellationToken);

                if (ledgerEntry != null)
                {
                    ledgerEntry.DebitAmount = newGrandTotal;
                    ledgerEntry.TransactionDate = dto.InvoiceDate;
                }

                // Step 8: LOG AUDIT RECORD
                var auditLog = new InvoiceEditAudit
                {
                    InvoiceID = invoice.InvoiceID,
                    InvoiceType = "Sales",
                    PreviousVersion = previousVersion,
                    NewVersion = invoice.Version,
                    PreviousItemCount = previousItemCount,
                    NewItemCount = processedNewItems.Count,
                    OldSubTotal = oldSubTotal,
                    NewSubTotal = newSubTotal,
                    OldDiscountTotal = oldDiscountTotal,
                    NewDiscountTotal = newDiscountTotal,
                    OldGrandTotal = oldGrandTotal,
                    NewGrandTotal = newGrandTotal,
                    EditReason = dto.EditReason.Trim(),
                    EditedBy = userId,
                    EditedAt = now
                };
                await _context.InvoiceEditAudits.AddAsync(auditLog, cancellationToken);

                await _salesRepository.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                _logger.LogInformation("Sales Invoice #{InvoiceNumber} updated (v{Version}). Old Total: {OldTotal}, New Total: {NewTotal}", invoice.InvoiceNumber, invoice.Version, oldGrandTotal, newGrandTotal);
                return OperationResult<int>.Ok(invoice.InvoiceID, $"Sales Invoice #{invoice.InvoiceNumber} updated to v{invoice.Version} successfully.");
            }
            catch (DbUpdateConcurrencyException ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogWarning(ex, "Concurrency conflict detected while editing Sales Invoice #{InvoiceNumber}", invoice.InvoiceNumber);
                return OperationResult<int>.Fail("This invoice has already been modified by another user. Please refresh and try again.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "Failed to update Sales Invoice #{InvoiceNumber}", invoice.InvoiceNumber);
                return OperationResult<int>.Fail($"An error occurred while updating the invoice: {ex.Message}");
            }
        }

        private sealed class InvoiceDiscountResolution
        {
            public bool Success { get; init; } = true;
            public string? ErrorMessage { get; init; }
            public string DiscountMode { get; init; } = "None";
            public string DiscountSource { get; init; } = "Automatic";
            public int? DiscountRuleID { get; init; }
            public string RuleName { get; init; } = string.Empty;
            public string DiscountType { get; init; } = "Percentage";
            public decimal DiscountValue { get; init; }
            public decimal HeaderDiscountAmount { get; init; }
            public decimal? MinimumOrderAmount { get; init; }
            public decimal? MaximumOrderAmount { get; init; }
        }

        private static string NormalizeDiscountMode(string? discountMode, int? appliedDiscountRuleId)
        {
            if (string.Equals(discountMode, "Manual", StringComparison.OrdinalIgnoreCase))
            {
                return "Manual";
            }

            if (string.Equals(discountMode, "Automatic", StringComparison.OrdinalIgnoreCase))
            {
                return "Automatic";
            }

            // Backward compatibility: existing Create UI posts AppliedDiscountRuleID without DiscountMode.
            if (appliedDiscountRuleId.HasValue && appliedDiscountRuleId.Value > 0)
            {
                return "Automatic";
            }

            return "None";
        }

        private async Task<InvoiceDiscountResolution> ResolveInvoiceDiscountAsync(
            string? discountMode,
            int? appliedDiscountRuleId,
            string? manualDiscountType,
            decimal manualDiscountValue,
            decimal paidSubTotal,
            bool autoPickFirstEligibleRule,
            CancellationToken cancellationToken)
        {
            string mode = NormalizeDiscountMode(discountMode, appliedDiscountRuleId);

            if (string.Equals(mode, "None", StringComparison.OrdinalIgnoreCase))
            {
                return new InvoiceDiscountResolution { DiscountMode = "None", HeaderDiscountAmount = 0m };
            }

            if (string.Equals(mode, "Manual", StringComparison.OrdinalIgnoreCase))
            {
                string type = string.Equals(manualDiscountType, "FixedAmount", StringComparison.OrdinalIgnoreCase)
                    ? "FixedAmount"
                    : "Percentage";

                if (manualDiscountValue < 0m)
                {
                    return new InvoiceDiscountResolution { Success = false, ErrorMessage = "Manual discount value cannot be negative." };
                }

                if (string.Equals(type, "Percentage", StringComparison.OrdinalIgnoreCase) && manualDiscountValue > 100m)
                {
                    return new InvoiceDiscountResolution { Success = false, ErrorMessage = "Manual discount percentage cannot exceed 100." };
                }

                decimal headerAmount = DiscountAllocationHelper.ComputeHeaderDiscount(type, manualDiscountValue, paidSubTotal);
                return new InvoiceDiscountResolution
                {
                    DiscountMode = "Manual",
                    DiscountSource = "Manual",
                    DiscountRuleID = null,
                    RuleName = "Manual Discount",
                    DiscountType = type,
                    DiscountValue = manualDiscountValue,
                    HeaderDiscountAmount = headerAmount,
                    MinimumOrderAmount = null,
                    MaximumOrderAmount = null
                };
            }

            // Automatic
            int? ruleId = appliedDiscountRuleId.HasValue && appliedDiscountRuleId.Value > 0 ? appliedDiscountRuleId : null;
            if (!ruleId.HasValue)
            {
                // No rule selected → no automatic discount (unless caller already auto-picked into appliedDiscountRuleId).
                _ = autoPickFirstEligibleRule;
                return new InvoiceDiscountResolution { DiscountMode = "None", HeaderDiscountAmount = 0m };
            }

            var rule = await _context.DiscountRules
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.DiscountRuleID == ruleId.Value && r.IsActive && !r.IsDeleted, cancellationToken);

            if (rule == null)
            {
                return new InvoiceDiscountResolution { Success = false, ErrorMessage = "Selected discount rule was not found or is inactive." };
            }

            if (paidSubTotal < rule.MinimumOrderAmount)
            {
                return new InvoiceDiscountResolution { DiscountMode = "None", HeaderDiscountAmount = 0m };
            }

            if (rule.MaximumOrderAmount.HasValue && paidSubTotal > rule.MaximumOrderAmount.Value)
            {
                return new InvoiceDiscountResolution { DiscountMode = "None", HeaderDiscountAmount = 0m };
            }

            decimal amount = DiscountAllocationHelper.ComputeHeaderDiscount(rule.DiscountType, rule.DiscountValue, paidSubTotal);
            return new InvoiceDiscountResolution
            {
                DiscountMode = amount > 0m ? "Automatic" : "None",
                DiscountSource = "Automatic",
                DiscountRuleID = rule.DiscountRuleID,
                RuleName = rule.RuleName,
                DiscountType = rule.DiscountType,
                DiscountValue = rule.DiscountValue,
                HeaderDiscountAmount = amount,
                MinimumOrderAmount = rule.MinimumOrderAmount,
                MaximumOrderAmount = rule.MaximumOrderAmount
            };
        }

        private static void ApplyDiscountAllocationToItems(
            List<CreateSalesItemDto> items,
            decimal headerDiscount,
            string discountType,
            decimal discountValue)
        {
            var normalIndexes = new List<DiscountAllocationHelper.LineGross>();
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (string.Equals(item.ItemType, "FREE", StringComparison.OrdinalIgnoreCase))
                {
                    item.DiscountAmount = 0m;
                    item.DiscountRate = 0m;
                    continue;
                }

                normalIndexes.Add(new DiscountAllocationHelper.LineGross
                {
                    Index = i,
                    Gross = item.Quantity * item.UnitPrice
                });
            }

            var allocations = DiscountAllocationHelper.Allocate(normalIndexes, headerDiscount, discountType, discountValue);
            foreach (var alloc in allocations)
            {
                items[alloc.Index].DiscountAmount = alloc.DiscountAmount;
                items[alloc.Index].DiscountRate = alloc.DiscountRate;
            }
        }

        private static void AppendAuthoritativeFreeItem(List<CreateSalesItemDto> authoritativeItems, PromotionSuggestionDto promo)
        {
            if (promo == null || promo.RewardQuantity <= 0)
            {
                return;
            }

            if (promo.IsCustomFreeItem)
            {
                string customName = string.IsNullOrWhiteSpace(promo.CustomFreeItemName)
                    ? (promo.FreeProductName ?? "Custom Item")
                    : promo.CustomFreeItemName.Trim();
                if (customName.StartsWith("Other:", StringComparison.OrdinalIgnoreCase))
                {
                    customName = customName.Substring(6).Trim();
                }

                authoritativeItems.Add(new CreateSalesItemDto
                {
                    ProductID = 0,
                    ProductUnitID = 0,
                    Quantity = promo.RewardQuantity,
                    UnitPrice = 0m,
                    DiscountAmount = 0m,
                    ItemType = "FREE",
                    PromotionID = promo.PromotionID,
                    IsCustomFreeItem = true,
                    CustomFreeItemName = customName
                });
                return;
            }

            if (promo.FreeProductID.HasValue && promo.FreeProductID.Value > 0 && promo.FreeUnitID > 0)
            {
                authoritativeItems.Add(new CreateSalesItemDto
                {
                    ProductID = promo.FreeProductID.Value,
                    ProductUnitID = promo.FreeUnitID,
                    Quantity = promo.RewardQuantity,
                    UnitPrice = 0m,
                    DiscountAmount = 0m,
                    ItemType = "FREE",
                    PromotionID = promo.PromotionID,
                    IsCustomFreeItem = false,
                    CustomFreeItemName = null
                });
            }
        }

        private async Task<OperationResult> ValidateInvoiceHeaderAsync(int brokerId, int salespersonId, CancellationToken cancellationToken)
        {
            if (brokerId <= 0)
            {
                return OperationResult.Fail("Please select a broker.");
            }

            if (salespersonId <= 0)
            {
                return OperationResult.Fail("Please select a salesperson.");
            }

            if (!await _salesRepository.BrokerExistsAsync(brokerId, cancellationToken))
            {
                return OperationResult.Fail("Selected broker does not exist or is inactive.");
            }

            if (!await _salesRepository.SalespersonExistsAsync(salespersonId, cancellationToken))
            {
                return OperationResult.Fail("Selected salesperson does not exist or is inactive.");
            }

            return OperationResult.Ok();
        }

        /// <summary>
        /// A sales invoice may contain products from any number of suppliers, so only the
        /// product's own existence and active state are validated here.
        /// </summary>
        private async Task<OperationResult> ValidateProductIsSellableAsync(int productId, CancellationToken cancellationToken)
        {
            if (productId <= 0)
            {
                return OperationResult.Ok();
            }

            var productCompanyId = await _salesRepository.GetProductCompanyIdAsync(productId, cancellationToken);
            if (!productCompanyId.HasValue)
            {
                return OperationResult.Fail($"Product ID {productId} does not exist or is inactive.");
            }

            return OperationResult.Ok();
        }
    }
}
