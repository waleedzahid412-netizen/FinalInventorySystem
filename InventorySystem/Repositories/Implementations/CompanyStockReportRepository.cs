using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using InventorySystem.Data;
using InventorySystem.DTOs.Common;
using InventorySystem.DTOs.Reports;
using InventorySystem.Repositories.Interfaces;

namespace InventorySystem.Repositories.Implementations
{
    public class CompanyStockReportRepository : ICompanyStockReportRepository
    {
        private readonly ApplicationDbContext _context;

        public CompanyStockReportRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<CompanyStockReportResultDto> GetPagedAsync(
            CompanyStockReportFilterDto filter,
            CancellationToken cancellationToken = default)
        {
            if (filter.PageNumber < 1) filter.PageNumber = 1;
            if (filter.PageSize < 1) filter.PageSize = 25;

            var products = _context.Products
                .AsNoTracking()
                .Where(p => p.IsActive);

            if (filter.CompanyID.HasValue && filter.CompanyID.Value > 0)
                products = products.Where(p => p.CompanyID == filter.CompanyID.Value);

            if (filter.CategoryID.HasValue && filter.CategoryID.Value > 0)
                products = products.Where(p => p.CategoryID == filter.CategoryID.Value);

            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                var term = filter.SearchTerm.Trim();
                products = products.Where(p =>
                    p.ProductName.Contains(term) ||
                    (p.SKU != null && p.SKU.Contains(term)));
            }

            // Project per-product stock once. Summary aggregates must run in memory —
            // SQL Server rejects SUM() over an expression that already contains SUM()/subquery.
            var query = products.Select(p => new CompanyStockReportRowDto
            {
                CompanyID = p.CompanyID,
                CompanyName = p.Company.CompanyName,
                ProductID = p.ProductID,
                ProductName = p.ProductName,
                SKU = p.SKU,
                BaseUnitName = p.BaseUnit != null ? p.BaseUnit.UnitName : "Units",
                CurrentStock = p.InventoryStocks.Sum(s => (decimal?)s.Quantity) ?? 0m,
                ReorderLevel = p.ReorderLevel,
                Status = string.Empty
            });

            var stockStatus = filter.StockStatus?.Trim();
            if (!string.IsNullOrEmpty(stockStatus))
            {
                query = stockStatus.ToLowerInvariant() switch
                {
                    "outofstock" => query.Where(r => r.CurrentStock <= 0),
                    "lowstock" => query.Where(r => r.CurrentStock > 0 && r.CurrentStock <= r.ReorderLevel),
                    "instock" => query.Where(r => r.CurrentStock > r.ReorderLevel),
                    _ => query
                };
            }

            var allItems = await query
                .OrderBy(r => r.CompanyName)
                .ThenBy(r => r.ProductName)
                .ToListAsync(cancellationToken);

            var summaryData = new CompanyStockReportSummaryDto
            {
                TotalProducts = allItems.Count,
                TotalStockQuantity = allItems.Sum(r => r.CurrentStock),
                LowStockCount = allItems.Count(r => r.CurrentStock > 0 && r.CurrentStock <= r.ReorderLevel),
                OutOfStockCount = allItems.Count(r => r.CurrentStock <= 0)
            };

            var items = allItems
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToList();

            return new CompanyStockReportResultDto
            {
                Rows = new PagedResult<CompanyStockReportRowDto>(
                    items,
                    summaryData.TotalProducts,
                    filter.PageNumber,
                    filter.PageSize),
                Summary = summaryData
            };
        }
    }
}
