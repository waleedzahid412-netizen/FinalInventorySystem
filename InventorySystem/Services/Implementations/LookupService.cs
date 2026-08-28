using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using InventorySystem.Data;
using InventorySystem.DTOs.Common;
using InventorySystem.DTOs.Products;
using InventorySystem.Services.Interfaces;

namespace InventorySystem.Services.Implementations
{
    public class LookupService : ILookupService
    {
        private readonly ApplicationDbContext _context;
        private readonly IUserCompanyAccessService _companyAccess;

        public LookupService(ApplicationDbContext context, IUserCompanyAccessService companyAccess)
        {
            _context = context;
            _companyAccess = companyAccess;
        }

        public async Task<List<LookupItemDto>> GetCategoriesAsync(int? companyId = null, CancellationToken cancellationToken = default)
        {
            var query = _context.Categories
                .AsNoTracking()
                .Where(c => c.IsActive);

            if (companyId.HasValue && companyId.Value > 0)
            {
                query = query.Where(c => c.CompanyID == companyId.Value);
            }
            else if (companyId.HasValue && companyId.Value <= 0)
            {
                return new List<LookupItemDto>();
            }

            return await query
                .OrderBy(c => c.Name)
                .Select(c => new LookupItemDto
                {
                    Id = c.CategoryID,
                    Name = c.Name
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<List<LookupItemDto>> GetUnitsAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Units
                .AsNoTracking()
                .Where(u => u.IsActive)
                .OrderBy(u => u.UnitName)
                .Select(u => new LookupItemDto
                {
                    Id = u.UnitID,
                    Name = u.UnitName
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<List<LookupItemDto>> GetCompaniesAsync(CancellationToken cancellationToken = default)
        {
            var query = _context.Companies
                .AsNoTracking()
                .Where(c => !c.IsDeleted);

            var userId = _companyAccess.GetCurrentUserId();
            if (userId.HasValue)
            {
                var profile = await _companyAccess.GetAccessProfileAsync(userId.Value, cancellationToken);
                if (!profile.IsUnrestricted)
                {
                    if (profile.AllowedCompanyIds.Count == 0)
                    {
                        return new List<LookupItemDto>();
                    }

                    query = query.Where(c => profile.AllowedCompanyIds.Contains(c.CompanyID));
                }
            }

            return await query
                .OrderBy(c => c.CompanyName)
                .Select(c => new LookupItemDto
                {
                    Id = c.CompanyID,
                    Name = c.CompanyName
                })
                .ToListAsync(cancellationToken);
        }

        public Task<List<LookupItemDto>> GetCustomersAsync(CancellationToken cancellationToken = default)
        {
            return GetCustomersAsync(null, null, cancellationToken);
        }

        public async Task<List<LookupItemDto>> GetCustomersAsync(int? areaId, int? subAreaId, CancellationToken cancellationToken = default)
        {
            var query = _context.Customers
                .AsNoTracking()
                .Where(c => !c.IsDeleted && c.IsActive);

            if (areaId.HasValue && areaId.Value > 0)
            {
                query = query.Where(c => c.AreaID == areaId.Value);
            }

            if (subAreaId.HasValue && subAreaId.Value > 0)
            {
                query = query.Where(c => c.SubAreaID == subAreaId.Value);
            }

            return await query
                .OrderBy(c => c.ShopName)
                .Select(c => new LookupItemDto
                {
                    Id = c.CustomerID,
                    Name = c.OwnerName != null && c.OwnerName != ""
                        ? c.ShopName + " (" + c.OwnerName + ")"
                        : c.ShopName
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<List<LookupItemDto>> GetAreasAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Areas
                .AsNoTracking()
                .Where(a => !a.IsDeleted && a.IsActive)
                .OrderBy(a => a.AreaName)
                .Select(a => new LookupItemDto
                {
                    Id = a.AreaID,
                    Name = a.AreaName
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<List<LookupItemDto>> GetSubAreasAsync(int? areaId = null, CancellationToken cancellationToken = default)
        {
            var query = _context.SubAreas
                .AsNoTracking()
                .Where(sa => !sa.IsDeleted && sa.IsActive);

            if (areaId.HasValue)
            {
                query = query.Where(sa => sa.AreaID == areaId.Value);
            }

            return await query
                .OrderBy(sa => sa.SubAreaName)
                .Select(sa => new LookupItemDto
                {
                    Id = sa.SubAreaID,
                    Name = sa.SubAreaName
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<ProductFormDropdownsDto> GetProductFormDropdownsAsync(int? companyId = null, CancellationToken cancellationToken = default)
        {
            var categories = companyId.HasValue && companyId.Value > 0
                ? await GetCategoriesAsync(companyId, cancellationToken)
                : new List<LookupItemDto>();
            var companies = await GetCompaniesAsync(cancellationToken);
            var units = await GetUnitsAsync(cancellationToken);

            return new ProductFormDropdownsDto
            {
                Categories = categories,
                Companies = companies,
                Units = units
            };
        }

        public async Task<List<LookupItemDto>> GetWarehousesAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Warehouses
                .AsNoTracking()
                .Where(w => !w.IsDeleted && w.IsActive)
                .OrderByDescending(w => w.IsMain)
                .ThenBy(w => w.Name)
                .Select(w => new LookupItemDto
                {
                    Id = w.WarehouseID,
                    Name = w.Name
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<int?> GetMainWarehouseIdAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Warehouses
                .AsNoTracking()
                .Where(w => !w.IsDeleted && w.IsActive && w.IsMain)
                .Select(w => (int?)w.WarehouseID)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<List<LookupItemDto>> GetProductsByCompanyAsync(int companyId, CancellationToken cancellationToken = default)
        {
            return await _context.Products
                .AsNoTracking()
                .Where(p => p.CompanyID == companyId && !p.IsDeleted && p.IsActive)
                .OrderBy(p => p.ProductName)
                .Select(p => new LookupItemDto
                {
                    Id = p.ProductID,
                    Name = string.IsNullOrWhiteSpace(p.SKU) ? p.ProductName : $"{p.ProductName} ({p.SKU})"
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<List<DTOs.Purchases.ProductUnitLookupDto>> GetProductUnitsAsync(int productId, CancellationToken cancellationToken = default)
        {
            return await _context.ProductUnits
                .AsNoTracking()
                .Where(pu => pu.ProductID == productId && !pu.IsDeleted && pu.IsActive)
                .OrderByDescending(pu => pu.IsDefaultPurchaseUnit)
                .ThenBy(pu => pu.Unit.UnitName)
                .Select(pu => new DTOs.Purchases.ProductUnitLookupDto
                {
                    ProductUnitID = pu.ProductUnitID,
                    UnitID = pu.UnitID,
                    UnitName = pu.Unit.UnitName,
                    ConversionToBaseUnit = pu.ConversionToBaseUnit,
                    PurchasePrice = pu.PurchasePrice,
                    IsDefaultPurchaseUnit = pu.IsDefaultPurchaseUnit
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<DTOs.Purchases.ProductInfoDto?> GetProductInfoAsync(int productId, CancellationToken cancellationToken = default)
        {
            return await _context.Products
                .AsNoTracking()
                .Where(p => p.ProductID == productId && !p.IsDeleted && p.IsActive)
                .Select(p => new DTOs.Purchases.ProductInfoDto
                {
                    ProductID = p.ProductID,
                    ProductName = p.ProductName,
                    SKU = p.SKU ?? string.Empty,
                    BaseUnitID = p.BaseUnitID,
                    BaseUnitName = p.BaseUnit != null ? p.BaseUnit.UnitName : string.Empty
                })
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<List<LookupItemDto>> GetSuppliersAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Suppliers
                .AsNoTracking()
                .Where(dp => !dp.IsDeleted && dp.IsActive)
                .OrderBy(dp => dp.Name)
                .Select(dp => new LookupItemDto
                {
                    Id = dp.SupplierID,
                    Name = dp.Name
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<List<LookupItemDto>> GetBookersAsync(int? companyId = null, CancellationToken cancellationToken = default)
        {
            var query = _context.Bookers
                .AsNoTracking()
                .Where(b => !b.IsDeleted && b.IsActive);

            if (companyId.HasValue && companyId.Value > 0)
            {
                query = query.Where(b => b.CompanyID == companyId.Value);
            }

            return await query
                .OrderBy(b => b.Name)
                .Select(b => new LookupItemDto
                {
                    Id = b.BookerID,
                    Name = b.Name
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<List<LookupItemDto>> GetSalespersonsAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Users
                .AsNoTracking()
                .Where(u => !u.IsDeleted && u.IsActive)
                .OrderBy(u => u.FullName)
                .Select(u => new LookupItemDto
                {
                    Id = u.UserID,
                    Name = string.IsNullOrWhiteSpace(u.FullName) ? u.Username : u.FullName
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<DTOs.Sales.CustomerInfoDto?> GetCustomerInfoAsync(int customerId, CancellationToken cancellationToken = default)
        {
            var customer = await _context.Customers
                .AsNoTracking()
                .Where(c => c.CustomerID == customerId && !c.IsDeleted && c.IsActive)
                .Select(c => new
                {
                    c.CustomerID,
                    c.ShopName,
                    c.OwnerName,
                    c.CreditLimit,
                    c.PreferredDiscountPercent,
                    c.AreaID,
                    AreaName = c.Area != null ? c.Area.AreaName : null,
                    c.SubAreaID,
                    SubAreaName = c.SubArea != null ? c.SubArea.SubAreaName : null
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (customer == null) return null;

            // Outstanding balance calculated from CustomerLedgers = SUM(Debit) - SUM(Credit)
            decimal totalDebit = await _context.CustomerLedgers
                .AsNoTracking()
                .Where(l => l.CustomerID == customerId)
                .SumAsync(l => (decimal?)l.DebitAmount, cancellationToken) ?? 0m;

            decimal totalCredit = await _context.CustomerLedgers
                .AsNoTracking()
                .Where(l => l.CustomerID == customerId)
                .SumAsync(l => (decimal?)l.CreditAmount, cancellationToken) ?? 0m;

            decimal outstanding = totalDebit - totalCredit;

            return new DTOs.Sales.CustomerInfoDto
            {
                CustomerID = customer.CustomerID,
                CustomerName = string.IsNullOrWhiteSpace(customer.OwnerName) ? customer.ShopName : $"{customer.ShopName} ({customer.OwnerName})",
                ShopName = customer.ShopName,
                CreditLimit = customer.CreditLimit,
                PreferredDiscountPercent = customer.PreferredDiscountPercent,
                CurrentOutstanding = outstanding,
                AreaID = customer.AreaID,
                AreaName = customer.AreaName,
                SubAreaID = customer.SubAreaID,
                SubAreaName = customer.SubAreaName
            };
        }

        public async Task<List<LookupItemDto>> GetProductsAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Products
                .AsNoTracking()
                .Where(p => !p.IsDeleted && p.IsActive)
                .OrderBy(p => p.ProductName)
                .Select(p => new LookupItemDto
                {
                    Id = p.ProductID,
                    Name = string.IsNullOrWhiteSpace(p.SKU) ? p.ProductName : $"{p.ProductName} ({p.SKU})"
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<DTOs.Sales.ProductUnitPriceDto?> GetProductUnitPriceAsync(int productId, int productUnitId, CancellationToken cancellationToken = default)
        {
            var productUnit = await _context.ProductUnits
                .AsNoTracking()
                .Include(pu => pu.Product)
                .Include(pu => pu.Unit)
                .FirstOrDefaultAsync(pu => pu.ProductID == productId && pu.ProductUnitID == productUnitId && !pu.IsDeleted && pu.IsActive, cancellationToken);

            if (productUnit == null) return null;

            decimal basePrice = productUnit.Product != null ? productUnit.Product.BaseSellingPrice : 0m;
            decimal unitPrice = productUnit.SellingPrice ?? (basePrice * productUnit.ConversionToBaseUnit);
            string unitName = productUnit.Unit != null ? productUnit.Unit.UnitName : "Unit";

            return new DTOs.Sales.ProductUnitPriceDto
            {
                ProductUnitID = productUnit.ProductUnitID,
                ProductID = productUnit.ProductID,
                UnitName = unitName,
                ConversionToBaseUnit = productUnit.ConversionToBaseUnit,
                UnitPrice = Math.Round(unitPrice, 2)
            };
        }

        public async Task<DTOs.Sales.AvailableStockDto> GetAvailableStockAsync(int productId, int warehouseId, int productUnitId, CancellationToken cancellationToken = default)
        {
            var stock = await _context.InventoryStocks
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.ProductID == productId && s.WarehouseID == warehouseId && !s.IsDeleted, cancellationToken);

            var productUnit = await _context.ProductUnits
                .AsNoTracking()
                .Include(pu => pu.Unit)
                .FirstOrDefaultAsync(pu => pu.ProductID == productId && pu.ProductUnitID == productUnitId && !pu.IsDeleted && pu.IsActive, cancellationToken);

            decimal baseStockQty = stock?.Quantity ?? 0m;
            decimal conversionFactor = productUnit?.ConversionToBaseUnit ?? 1m;
            string unitName = productUnit?.Unit?.UnitName ?? "Unit";

            decimal availableDisplayStock = conversionFactor > 0 ? Math.Floor(baseStockQty / conversionFactor) : baseStockQty;

            return new DTOs.Sales.AvailableStockDto
            {
                ProductID = productId,
                WarehouseID = warehouseId,
                ProductUnitID = productUnitId,
                AvailableStock = Math.Max(0, availableDisplayStock),
                AvailableBaseStock = Math.Max(0, baseStockQty),
                UnitName = unitName
            };
        }
    }
}
