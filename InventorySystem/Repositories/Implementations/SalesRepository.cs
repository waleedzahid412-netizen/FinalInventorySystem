using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using InventorySystem.Data;
using InventorySystem.DTOs.Common;
using InventorySystem.DTOs.Sales;
using InventorySystem.Models.Entities;
using InventorySystem.Repositories.Interfaces;

namespace InventorySystem.Repositories.Implementations
{
    public class SalesRepository : ISalesRepository
    {
        private readonly ApplicationDbContext _context;

        public SalesRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        #region 1. Query Operations

        public async Task<PagedResult<SalesListDto>> GetPagedAsync(SalesFilterDto filter, CancellationToken cancellationToken = default)
        {
            var query = _context.SalesInvoices
                .AsNoTracking()
                .Where(si => !si.IsDeleted);

            if (!string.IsNullOrWhiteSpace(filter.InvoiceNumber))
            {
                var invNum = filter.InvoiceNumber.Trim();
                query = query.Where(si => si.InvoiceNumber.Contains(invNum));
            }

            if (filter.CustomerID.HasValue && filter.CustomerID.Value > 0)
            {
                query = query.Where(si => si.CustomerID == filter.CustomerID.Value);
            }

            if (filter.WarehouseID.HasValue && filter.WarehouseID.Value > 0)
            {
                query = query.Where(si => si.WarehouseID == filter.WarehouseID.Value);
            }

            if (filter.DeliveryPersonID.HasValue && filter.DeliveryPersonID.Value > 0)
            {
                query = query.Where(si => si.DeliveryPersonID == filter.DeliveryPersonID.Value);
            }

            if (filter.DateFrom.HasValue)
            {
                query = query.Where(si => si.InvoiceDate >= filter.DateFrom.Value);
            }

            if (filter.DateTo.HasValue)
            {
                var endOfDay = filter.DateTo.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(si => si.InvoiceDate <= endOfDay);
            }

            if (!string.IsNullOrWhiteSpace(filter.PaymentStatus))
            {
                query = query.Where(si => si.PaymentStatus == filter.PaymentStatus);
            }

            // Sorting: newest first
            query = query.OrderByDescending(si => si.InvoiceDate).ThenByDescending(si => si.InvoiceID);

            int totalCount = await query.CountAsync(cancellationToken);

            var items = await query
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .Select(si => new SalesListDto
                {
                    InvoiceID = si.InvoiceID,
                    InvoiceNumber = si.InvoiceNumber,
                    CustomerID = si.CustomerID,
                    CustomerName = string.IsNullOrWhiteSpace(si.Customer.OwnerName) ? si.Customer.ShopName : $"{si.Customer.ShopName} ({si.Customer.OwnerName})",
                    DeliveryPersonName = si.DeliveryPerson != null ? si.DeliveryPerson.Name : null,
                    InvoiceDate = si.InvoiceDate,
                    SubTotal = si.SubTotal,
                    DiscountTotal = si.DiscountTotal,
                    TaxTotal = si.TaxTotal,
                    GrandTotal = si.GrandTotal,
                    PaidAmount = si.PaidAmount,
                    ReturnedAmount = si.SalesReturns.Where(r => !r.IsDeleted).Sum(r => (decimal?)r.NetRefundAmount) ?? 0m,
                    PaymentStatus = si.PaymentStatus,
                    IsLocked = si.IsLocked,
                    CreatedAt = si.CreatedAt
                })
                .ToListAsync(cancellationToken);

            foreach (var item in items)
            {
                if (item.ReturnedAmount > 0)
                {
                    decimal netRemaining = Math.Max(0m, item.GrandTotal - item.PaidAmount - item.ReturnedAmount);
                    if (netRemaining <= 0.001m)
                    {
                        item.PaymentStatus = "PAID";
                    }
                    else
                    {
                        item.PaymentStatus = "PARTIAL";
                    }
                }
            }

            return new PagedResult<SalesListDto>(items, totalCount, filter.PageNumber, filter.PageSize);
        }

        public async Task<SalesDetailsDto?> GetDetailsByIdAsync(int salesInvoiceId, CancellationToken cancellationToken = default)
        {
            var header = await _context.SalesInvoices
                .AsNoTracking()
                .Where(si => si.InvoiceID == salesInvoiceId && !si.IsDeleted)
                .Select(si => new SalesHeaderDto
                {
                    InvoiceID = si.InvoiceID,
                    InvoiceNumber = si.InvoiceNumber,
                    InvoiceDate = si.InvoiceDate,
                    CustomerID = si.CustomerID,
                    CustomerName = string.IsNullOrWhiteSpace(si.Customer.OwnerName) ? si.Customer.ShopName : $"{si.Customer.ShopName} ({si.Customer.OwnerName})",
                    ShopName = si.Customer.ShopName,
                    CustomerAddress = si.Customer.Address,
                    CompanyID = si.CompanyID,
                    CompanyName = si.Company != null ? si.Company.CompanyName : null,
                    BrokerID = si.BrokerID,
                    BrokerName = si.Broker != null ? si.Broker.Name : null,
                    PaymentMode = si.CustomerPayments
                        .Where(p => !p.IsDeleted)
                        .OrderBy(p => p.PaymentDate)
                        .Select(p => p.PaymentMethod)
                        .FirstOrDefault()
                        ?? (si.PaidAmount > 0 ? "Cash" : "Credit"),
                    SalespersonID = si.SalespersonID,
                    SalespersonName = si.SalespersonUser != null ? (si.SalespersonUser.FullName ?? si.SalespersonUser.Username) : null,
                    WarehouseID = si.WarehouseID,
                    WarehouseName = si.Warehouse.Name,
                    DeliveryPersonID = si.DeliveryPersonID,
                    DeliveryPersonName = si.DeliveryPerson != null ? si.DeliveryPerson.Name : null,
                    AreaID = si.AreaID,
                    AreaName = si.Area != null ? si.Area.AreaName : null,
                    SubAreaID = si.SubAreaID,
                    SubAreaName = si.SubArea != null ? si.SubArea.SubAreaName : null,
                    SubTotal = si.SubTotal,
                    DiscountTotal = si.DiscountTotal,
                    TaxTotal = si.TaxTotal,
                    GrandTotal = si.GrandTotal,
                    PaidAmount = si.PaidAmount,
                    ReturnedAmount = si.SalesReturns.Where(r => !r.IsDeleted).Sum(r => (decimal?)r.NetRefundAmount) ?? 0m,
                    PaymentStatus = si.PaymentStatus,
                    AppliedDiscountRuleID = si.InvoiceDiscounts.Select(id => (int?)id.DiscountRuleID).FirstOrDefault(),
                    DiscountMode = si.DiscountMode,
                    IsLocked = si.IsLocked,
                    CreatedByUserName = si.CreatedByUser.FullName ?? si.CreatedByUser.Username,
                    CreatedAt = si.CreatedAt
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (header == null) return null;

            if (header.ReturnedAmount > 0)
            {
                decimal netRemaining = Math.Max(0m, header.GrandTotal - header.PaidAmount - header.ReturnedAmount);
                if (netRemaining <= 0.001m)
                {
                    header.PaymentStatus = "PAID";
                }
                else
                {
                    header.PaymentStatus = "PARTIAL";
                }
            }

            var items = await _context.SalesInvoiceItems
                .AsNoTracking()
                .Where(sii => sii.InvoiceID == salesInvoiceId && !sii.IsDeleted)
                .Select(sii => new SalesItemDto
                {
                    InvoiceItemID = sii.InvoiceItemID,
                    InvoiceID = sii.InvoiceID,
                    ProductID = sii.ProductID,
                    ProductName = !string.IsNullOrEmpty(sii.CustomItemName)
                        ? ("Other: " + sii.CustomItemName)
                        : (sii.Product != null ? sii.Product.ProductName : string.Empty),
                    SKU = sii.Product != null ? sii.Product.SKU : null,
                    ProductUnitID = sii.ProductUnitID,
                    UnitName = sii.ProductUnit != null && sii.ProductUnit.Unit != null
                        ? sii.ProductUnit.Unit.UnitName
                        : (!string.IsNullOrEmpty(sii.CustomItemName) ? "Item" : string.Empty),
                    Quantity = sii.Quantity,
                    ConvertedQuantity = sii.ConvertedQuantity,
                    BaseUnitName = sii.Product != null && sii.Product.BaseUnit != null
                        ? sii.Product.BaseUnit.UnitName
                        : (!string.IsNullOrEmpty(sii.CustomItemName) ? "Item" : string.Empty),
                    UnitPrice = sii.UnitPrice,
                    DiscountRate = sii.DiscountRate,
                    DiscountAmount = sii.DiscountAmount,
                    ItemType = sii.ItemType,
                    PromotionID = sii.PromotionID,
                    CustomItemName = sii.CustomItemName
                })
                .ToListAsync(cancellationToken);

            var financial = new SalesFinancialSummaryDto
            {
                SubTotal = header.SubTotal,
                DiscountTotal = header.DiscountTotal,
                TaxTotal = header.TaxTotal,
                GrandTotal = header.GrandTotal,
                PaidAmount = header.PaidAmount
            };

            return new SalesDetailsDto
            {
                Header = header,
                Items = items,
                Financial = financial
            };
        }

        public async Task<SalesInvoice?> GetByIdAsync(int salesInvoiceId, CancellationToken cancellationToken = default)
        {
            return await _context.SalesInvoices
                .Include(si => si.Customer)
                .Include(si => si.Warehouse)
                .Include(si => si.Items)
                .FirstOrDefaultAsync(si => si.InvoiceID == salesInvoiceId && !si.IsDeleted, cancellationToken);
        }

        #endregion

        #region 2. Validation / Existence Checks

        public async Task<bool> ExistsByInvoiceNumberAsync(string invoiceNumber, int? excludeId = null, CancellationToken cancellationToken = default)
        {
            var query = _context.SalesInvoices.AsNoTracking().Where(si => !si.IsDeleted && si.InvoiceNumber == invoiceNumber);
            if (excludeId.HasValue)
            {
                query = query.Where(si => si.InvoiceID != excludeId.Value);
            }
            return await query.AnyAsync(cancellationToken);
        }

        public async Task<Customer?> GetCustomerForInvoiceAsync(int customerId, CancellationToken cancellationToken = default)
        {
            return await _context.Customers
                .Include(c => c.Area)
                .Include(c => c.SubArea)
                .FirstOrDefaultAsync(c => c.CustomerID == customerId && !c.IsDeleted && c.IsActive, cancellationToken);
        }

        public async Task<bool> WarehouseExistsAsync(int warehouseId, CancellationToken cancellationToken = default)
        {
            return await _context.Warehouses.AsNoTracking().AnyAsync(w => w.WarehouseID == warehouseId && !w.IsDeleted && w.IsActive, cancellationToken);
        }

        public async Task<bool> DeliveryPersonExistsAsync(int deliveryPersonId, CancellationToken cancellationToken = default)
        {
            return await _context.DeliveryPersons.AsNoTracking().AnyAsync(dp => dp.DeliveryPersonID == deliveryPersonId && !dp.IsDeleted && dp.IsActive, cancellationToken);
        }

        public async Task<bool> CompanyExistsAsync(int companyId, CancellationToken cancellationToken = default)
        {
            return await _context.Companies.AsNoTracking().AnyAsync(c => c.CompanyID == companyId && !c.IsDeleted, cancellationToken);
        }

        public async Task<bool> BrokerExistsAsync(int brokerId, CancellationToken cancellationToken = default)
        {
            return await _context.Brokers.AsNoTracking().AnyAsync(b => b.BrokerID == brokerId && !b.IsDeleted && b.IsActive, cancellationToken);
        }

        public async Task<bool> SalespersonExistsAsync(int userId, CancellationToken cancellationToken = default)
        {
            return await _context.Users.AsNoTracking().AnyAsync(u => u.UserID == userId && !u.IsDeleted && u.IsActive, cancellationToken);
        }

        public async Task<int?> GetProductCompanyIdAsync(int productId, CancellationToken cancellationToken = default)
        {
            return await _context.Products
                .AsNoTracking()
                .Where(p => p.ProductID == productId && !p.IsDeleted && p.IsActive)
                .Select(p => (int?)p.CompanyID)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<bool> ProductExistsAsync(int productId, CancellationToken cancellationToken = default)
        {
            return await _context.Products.AsNoTracking().AnyAsync(p => p.ProductID == productId && !p.IsDeleted && p.IsActive, cancellationToken);
        }

        public async Task<ProductUnit?> GetProductUnitAsync(int productUnitId, CancellationToken cancellationToken = default)
        {
            return await _context.ProductUnits
                .Include(pu => pu.Product)
                .Include(pu => pu.Unit)
                .FirstOrDefaultAsync(pu => pu.ProductUnitID == productUnitId && !pu.IsDeleted && pu.IsActive, cancellationToken);
        }

        #endregion

        #region 3. Financial & History Queries

        public async Task<SalesFinancialSummaryDto?> GetFinancialSummaryAsync(int salesInvoiceId, CancellationToken cancellationToken = default)
        {
            return await _context.SalesInvoices
                .AsNoTracking()
                .Where(si => si.InvoiceID == salesInvoiceId && !si.IsDeleted)
                .Select(si => new SalesFinancialSummaryDto
                {
                    SubTotal = si.SubTotal,
                    DiscountTotal = si.DiscountTotal,
                    TaxTotal = si.TaxTotal,
                    GrandTotal = si.GrandTotal,
                    PaidAmount = si.PaidAmount
                })
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<PagedResult<SalesPaymentHistoryDto>> GetPaymentHistoryAsync(int salesInvoiceId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        {
            var query = _context.CustomerPayments
                .AsNoTracking()
                .Where(p => p.InvoiceID == salesInvoiceId)
                .OrderByDescending(p => p.PaymentDate);

            int totalCount = await query.CountAsync(cancellationToken);

            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new SalesPaymentHistoryDto
                {
                    CustomerPaymentID = p.CustomerPaymentID,
                    PaymentNumber = $"PAY-{p.CustomerPaymentID:D5}",
                    PaymentDate = p.PaymentDate,
                    Amount = p.Amount,
                    PaymentMethod = p.PaymentMethod,
                    ReferenceNumber = p.ReferenceNumber,
                    CreatedByUserName = p.ReceivedByUser != null ? (p.ReceivedByUser.FullName ?? p.ReceivedByUser.Username) : "System"
                })
                .ToListAsync(cancellationToken);

            return new PagedResult<SalesPaymentHistoryDto>(items, totalCount, pageNumber, pageSize);
        }

        public async Task<PagedResult<SalesLedgerEntryDto>> GetLedgerByInvoiceAsync(int salesInvoiceId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        {
            var query = _context.CustomerLedgers
                .AsNoTracking()
                .Where(l => l.SalesInvoiceID == salesInvoiceId)
                .OrderByDescending(l => l.TransactionDate);

            int totalCount = await query.CountAsync(cancellationToken);

            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(l => new SalesLedgerEntryDto
                {
                    CustomerLedgerID = l.CustomerLedgerID,
                    TransactionDate = l.TransactionDate,
                    TransactionType = l.TransactionType,
                    DebitAmount = l.DebitAmount,
                    CreditAmount = l.CreditAmount,
                    Description = l.Description,
                    CreatedByUserName = l.CreatedByUser != null ? (l.CreatedByUser.FullName ?? l.CreatedByUser.Username) : "System"
                })
                .ToListAsync(cancellationToken);

            return new PagedResult<SalesLedgerEntryDto>(items, totalCount, pageNumber, pageSize);
        }

        #endregion

        #region 4. Write Operations

        public async Task AddInvoiceAsync(SalesInvoice invoice, CancellationToken cancellationToken = default)
        {
            await _context.SalesInvoices.AddAsync(invoice, cancellationToken);
        }

        public async Task AddInvoiceItemAsync(SalesInvoiceItem item, CancellationToken cancellationToken = default)
        {
            await _context.SalesInvoiceItems.AddAsync(item, cancellationToken);
        }

        public async Task<InventoryStock?> GetInventoryStockTrackedAsync(int productId, int warehouseId, CancellationToken cancellationToken = default)
        {
            return await _context.InventoryStocks
                .FirstOrDefaultAsync(s => s.ProductID == productId && s.WarehouseID == warehouseId, cancellationToken);
        }

        public async Task AddInventoryStockAsync(InventoryStock stock, CancellationToken cancellationToken = default)
        {
            await _context.InventoryStocks.AddAsync(stock, cancellationToken);
        }

        public async Task AddInventoryTransactionAsync(InventoryTransaction transaction, CancellationToken cancellationToken = default)
        {
            await _context.InventoryTransactions.AddAsync(transaction, cancellationToken);
        }

        public async Task AddCustomerLedgerAsync(CustomerLedger ledger, CancellationToken cancellationToken = default)
        {
            await _context.CustomerLedgers.AddAsync(ledger, cancellationToken);
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        #endregion
    }
}
