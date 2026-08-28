using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using InventorySystem.Data;
using InventorySystem.DTOs.Common;
using InventorySystem.DTOs.Products;
using InventorySystem.Models.Entities;
using InventorySystem.Repositories.Interfaces;

namespace InventorySystem.Repositories.Implementations
{
    public class ProductRepository : IProductRepository
    {
        private readonly ApplicationDbContext _context;

        public ProductRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<PagedResult<Product>> GetPagedAsync(ProductFilterDto filter, CancellationToken cancellationToken = default)
        {
            var query = _context.Products
                .Include(p => p.Category)
                .Include(p => p.Company)
                .Include(p => p.BaseUnit)
                .Include(p => p.ProductUnits)
                    .ThenInclude(pu => pu.Unit)
                .AsNoTracking()
                .AsQueryable();

            // Search filter
            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                var term = filter.SearchTerm.Trim();
                query = query.Where(p =>
                    p.ProductName.Contains(term) ||
                    (p.SKU != null && p.SKU.Contains(term)) ||
                    (p.Barcode != null && p.Barcode.Contains(term)));
            }

            if (!string.IsNullOrWhiteSpace(filter.SKU))
            {
                query = query.Where(p => p.SKU != null && p.SKU.Contains(filter.SKU.Trim()));
            }

            if (!string.IsNullOrWhiteSpace(filter.Barcode))
            {
                query = query.Where(p => p.Barcode != null && p.Barcode.Contains(filter.Barcode.Trim()));
            }

            if (filter.CategoryID.HasValue && filter.CategoryID.Value > 0)
            {
                query = query.Where(p => p.CategoryID == filter.CategoryID.Value);
            }

            if (filter.CompanyID.HasValue && filter.CompanyID.Value > 0)
            {
                query = query.Where(p => p.CompanyID == filter.CompanyID.Value);
            }

            if (filter.IsActive.HasValue)
            {
                query = query.Where(p => p.IsActive == filter.IsActive.Value);
            }

            // Sorting
            query = filter.SortBy?.ToLower() switch
            {
                "sku" => filter.IsAscending ? query.OrderBy(p => p.SKU) : query.OrderByDescending(p => p.SKU),
                "basesellingprice" or "price" => filter.IsAscending ? query.OrderBy(p => p.BaseSellingPrice) : query.OrderByDescending(p => p.BaseSellingPrice),
                "createdat" => filter.IsAscending ? query.OrderBy(p => p.CreatedAt) : query.OrderByDescending(p => p.CreatedAt),
                _ => filter.IsAscending ? query.OrderBy(p => p.ProductName) : query.OrderByDescending(p => p.ProductName)
            };

            int totalCount = await query.CountAsync(cancellationToken);

            var items = await query
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync(cancellationToken);

            return new PagedResult<Product>(items, totalCount, filter.PageNumber, filter.PageSize);
        }

        public async Task<Product?> GetByIdWithUnitsAsync(int productId, CancellationToken cancellationToken = default)
        {
            return await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Company)
                .Include(p => p.BaseUnit)
                .Include(p => p.ProductUnits)
                    .ThenInclude(pu => pu.Unit)
                .FirstOrDefaultAsync(p => p.ProductID == productId, cancellationToken);
        }

        public async Task<bool> IsSkuUniqueAsync(string sku, int? excludeProductId = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sku)) return true;
            var normalizedSku = sku.Trim().ToLower();

            var query = _context.Products.AsNoTracking().Where(p => p.SKU != null && p.SKU.ToLower() == normalizedSku);
            if (excludeProductId.HasValue)
            {
                query = query.Where(p => p.ProductID != excludeProductId.Value);
            }

            return !await query.AnyAsync(cancellationToken);
        }

        public async Task<bool> IsBarcodeUniqueAsync(string barcode, int? excludeProductId = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(barcode)) return true;
            var normalizedBarcode = barcode.Trim().ToLower();

            var query = _context.Products.AsNoTracking().Where(p => p.Barcode != null && p.Barcode.ToLower() == normalizedBarcode);
            if (excludeProductId.HasValue)
            {
                query = query.Where(p => p.ProductID != excludeProductId.Value);
            }

            return !await query.AnyAsync(cancellationToken);
        }

        public async Task AddAsync(Product product, CancellationToken cancellationToken = default)
        {
            await _context.Products.AddAsync(product, cancellationToken);
        }

        public Task UpdateAsync(Product product, CancellationToken cancellationToken = default)
        {
            _context.Products.Update(product);
            return Task.CompletedTask;
        }

        public async Task SoftDeleteAsync(int productId, int userId, CancellationToken cancellationToken = default)
        {
            var product = await _context.Products
                .Include(p => p.ProductUnits)
                .FirstOrDefaultAsync(p => p.ProductID == productId, cancellationToken);

            if (product != null)
            {
                var now = DateTime.UtcNow;
                product.IsDeleted = true;
                product.DeletedAt = now;
                product.UpdatedBy = userId;
                product.UpdatedAt = now;

                foreach (var unit in product.ProductUnits)
                {
                    unit.IsDeleted = true;
                    unit.DeletedAt = now;
                    unit.UpdatedBy = userId;
                    unit.UpdatedAt = now;
                }
            }
        }

        public async Task<InventorySummaryDto?> GetInventorySummaryAsync(int productId, CancellationToken cancellationToken = default)
        {
            var product = await _context.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.ProductID == productId, cancellationToken);

            if (product == null) return null;

            var totalStock = await _context.InventoryStocks
                .Where(s => s.ProductID == productId)
                .SumAsync(s => (decimal?)s.Quantity, cancellationToken) ?? 0m;

            var totalSalesQty = await _context.SalesInvoiceItems
                .Where(s => s.ProductID == productId)
                .SumAsync(s => (decimal?)s.ConvertedQuantity, cancellationToken) ?? 0m;

            var totalPurchaseQty = await _context.PurchaseInvoiceItems
                .Where(p => p.ProductID == productId)
                .SumAsync(p => (decimal?)p.ConvertedQuantity, cancellationToken) ?? 0m;

            var lastPurchaseDate = await _context.PurchaseInvoiceItems
                .Where(p => p.ProductID == productId)
                .Select(p => (DateTime?)p.PurchaseInvoice.InvoiceDate)
                .OrderByDescending(d => d)
                .FirstOrDefaultAsync(cancellationToken);

            var lastSaleDate = await _context.SalesInvoiceItems
                .Where(s => s.ProductID == productId)
                .Select(s => (DateTime?)s.SalesInvoice.InvoiceDate)
                .OrderByDescending(d => d)
                .FirstOrDefaultAsync(cancellationToken);

            return new InventorySummaryDto
            {
                ProductID = product.ProductID,
                ProductName = product.ProductName,
                TotalStockQuantity = totalStock,
                TotalInventoryValue = totalStock * product.AveragePurchaseCost,
                ReorderLevel = product.ReorderLevel,
                BaseSellingPrice = product.BaseSellingPrice,
                AveragePurchaseCost = product.AveragePurchaseCost,
                LastPurchaseDate = lastPurchaseDate,
                LastSaleDate = lastSaleDate,
                TotalSalesQuantity = totalSalesQty,
                TotalPurchaseQuantity = totalPurchaseQty
            };
        }

        public async Task<List<WarehouseStockSummaryDto>> GetWarehouseStocksAsync(int productId, CancellationToken cancellationToken = default)
        {
            return await _context.InventoryStocks
                .AsNoTracking()
                .Include(s => s.Warehouse)
                .Where(s => s.ProductID == productId)
                .Select(s => new WarehouseStockSummaryDto
                {
                    WarehouseID = s.WarehouseID,
                    WarehouseName = s.Warehouse.Name,
                    Quantity = s.Quantity
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<PagedResult<ProductPurchaseHistoryDto>> GetPurchaseHistoryAsync(int productId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        {
            var query = _context.PurchaseInvoiceItems
                .AsNoTracking()
                .Include(pii => pii.PurchaseInvoice)
                    .ThenInclude(pi => pi.Company)
                .Include(pii => pii.ProductUnit)
                    .ThenInclude(pu => pu.Unit)
                .Where(pii => pii.ProductID == productId)
                .OrderByDescending(pii => pii.PurchaseInvoice.InvoiceDate);

            int totalCount = await query.CountAsync(cancellationToken);

            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(pii => new ProductPurchaseHistoryDto
                {
                    PurchaseInvoiceID = pii.PurchaseInvoiceID,
                    InvoiceNumber = pii.PurchaseInvoice.InvoiceNumber,
                    InvoiceDate = pii.PurchaseInvoice.InvoiceDate,
                    CompanyName = pii.PurchaseInvoice.Company.CompanyName,
                    UnitName = pii.ProductUnit.Unit.UnitName,
                    Quantity = pii.Quantity,
                    ConvertedQuantity = pii.ConvertedQuantity,
                    UnitCost = pii.UnitCost,
                    TotalCost = pii.TotalCost
                })
                .ToListAsync(cancellationToken);

            return new PagedResult<ProductPurchaseHistoryDto>(items, totalCount, pageNumber, pageSize);
        }

        public async Task<PagedResult<ProductSalesHistoryDto>> GetSalesHistoryAsync(int productId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        {
            var query = _context.SalesInvoiceItems
                .AsNoTracking()
                .Include(sii => sii.SalesInvoice)
                    .ThenInclude(si => si.Customer)
                .Include(sii => sii.ProductUnit)
                    .ThenInclude(pu => pu.Unit)
                .Where(sii => sii.ProductID == productId)
                .OrderByDescending(sii => sii.SalesInvoice.InvoiceDate);

            int totalCount = await query.CountAsync(cancellationToken);

            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(sii => new ProductSalesHistoryDto
                {
                    InvoiceID = sii.InvoiceID,
                    InvoiceNumber = sii.SalesInvoice.InvoiceNumber,
                    InvoiceDate = sii.SalesInvoice.InvoiceDate,
                    CustomerName = sii.SalesInvoice.Customer.ShopName,
                    UnitName = sii.ProductUnit.Unit.UnitName,
                    Quantity = sii.Quantity,
                    ConvertedQuantity = sii.ConvertedQuantity,
                    UnitPrice = sii.UnitPrice,
                    TotalPrice = (sii.Quantity * sii.UnitPrice) - sii.DiscountAmount
                })
                .ToListAsync(cancellationToken);

            return new PagedResult<ProductSalesHistoryDto>(items, totalCount, pageNumber, pageSize);
        }

        public async Task<PagedResult<ProductInventoryTransactionDto>> GetInventoryTransactionsAsync(int productId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        {
            var query = _context.InventoryTransactions
                .AsNoTracking()
                .Include(t => t.Warehouse)
                .Where(t => t.ProductID == productId)
                .OrderByDescending(t => t.CreatedAt);

            int totalCount = await query.CountAsync(cancellationToken);

            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(t => new ProductInventoryTransactionDto
                {
                    TransactionID = t.TransactionID,
                    TransactionDate = t.CreatedAt,
                    TransactionType = t.TransactionType,
                    WarehouseName = t.Warehouse.Name,
                    Quantity = t.Quantity,
                    ReferenceNumber = t.ReferenceNumber
                })
                .ToListAsync(cancellationToken);

            return new PagedResult<ProductInventoryTransactionDto>(items, totalCount, pageNumber, pageSize);
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
