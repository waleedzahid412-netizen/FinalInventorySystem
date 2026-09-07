using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using InventorySystem.Data;
using InventorySystem.DTOs.Common;
using InventorySystem.DTOs.Warehouses;
using InventorySystem.Models.Entities;
using InventorySystem.Repositories.Interfaces;

namespace InventorySystem.Repositories.Implementations
{
    public class WarehouseRepository : IWarehouseRepository
    {
        private readonly ApplicationDbContext _context;

        public WarehouseRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<PagedResult<Warehouse>> GetPagedAsync(WarehouseFilterDto filter, CancellationToken cancellationToken = default)
        {
            var query = _context.Warehouses.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                var term = filter.SearchTerm.Trim();
                query = query.Where(w => w.Name.Contains(term) || (w.Address != null && w.Address.Contains(term)));
            }

            if (filter.IsActive.HasValue)
            {
                query = query.Where(w => w.IsActive == filter.IsActive.Value);
            }

            if (filter.IsMain.HasValue)
            {
                query = query.Where(w => w.IsMain == filter.IsMain.Value);
            }

            query = filter.SortBy?.ToLower() switch
            {
                "createdat" => filter.IsAscending
                    ? query.OrderBy(w => w.CreatedAt)
                    : query.OrderByDescending(w => w.CreatedAt),
                "status" => filter.IsAscending
                    ? query.OrderBy(w => w.IsActive).ThenBy(w => w.Name)
                    : query.OrderByDescending(w => w.IsActive).ThenBy(w => w.Name),
                "ismain" => filter.IsAscending
                    ? query.OrderByDescending(w => w.IsMain).ThenBy(w => w.Name)
                    : query.OrderBy(w => w.IsMain).ThenBy(w => w.Name),
                _ => filter.IsAscending
                    ? query.OrderBy(w => w.Name)
                    : query.OrderByDescending(w => w.Name)
            };

            int totalCount = await query.CountAsync(cancellationToken);

            var items = await query
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync(cancellationToken);

            return new PagedResult<Warehouse>(items, totalCount, filter.PageNumber, filter.PageSize);
        }

        public async Task<Warehouse?> GetByIdAsync(int warehouseId, CancellationToken cancellationToken = default)
        {
            return await _context.Warehouses
                .AsNoTracking()
                .FirstOrDefaultAsync(w => w.WarehouseID == warehouseId, cancellationToken);
        }

        public async Task<bool> ExistsByNameAsync(string name, int? excludeWarehouseId = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            var normalizedName = name.Trim().ToLower();

            var query = _context.Warehouses
                .AsNoTracking()
                .Where(w => w.Name.ToLower() == normalizedName);

            if (excludeWarehouseId.HasValue)
            {
                query = query.Where(w => w.WarehouseID != excludeWarehouseId.Value);
            }

            return await query.AnyAsync(cancellationToken);
        }

        public async Task<bool> IsInUseAsync(int warehouseId, CancellationToken cancellationToken = default)
        {
            if (await _context.InventoryStocks.AnyAsync(s => s.WarehouseID == warehouseId, cancellationToken))
                return true;

            if (await _context.InventoryTransactions.AnyAsync(t => t.WarehouseID == warehouseId, cancellationToken))
                return true;

            if (await _context.PurchaseInvoices.AnyAsync(p => p.WarehouseID == warehouseId, cancellationToken))
                return true;

            return await _context.SalesInvoices.AnyAsync(s => s.WarehouseID == warehouseId, cancellationToken);
        }

        public async Task<bool> HasAnyMainAsync(int? excludeWarehouseId = null, CancellationToken cancellationToken = default)
        {
            var query = _context.Warehouses.AsNoTracking().Where(w => w.IsMain);

            if (excludeWarehouseId.HasValue)
            {
                query = query.Where(w => w.WarehouseID != excludeWarehouseId.Value);
            }

            return await query.AnyAsync(cancellationToken);
        }

        public async Task ClearMainFlagsExceptAsync(int? keepWarehouseId, CancellationToken cancellationToken = default)
        {
            var query = _context.Warehouses.Where(w => w.IsMain);

            if (keepWarehouseId.HasValue)
            {
                query = query.Where(w => w.WarehouseID != keepWarehouseId.Value);
            }

            var mains = await query.ToListAsync(cancellationToken);
            foreach (var warehouse in mains)
            {
                warehouse.IsMain = false;
            }
        }

        public async Task AddAsync(Warehouse warehouse, CancellationToken cancellationToken = default)
        {
            await _context.Warehouses.AddAsync(warehouse, cancellationToken);
        }

        public Task UpdateAsync(Warehouse warehouse, CancellationToken cancellationToken = default)
        {
            _context.Warehouses.Update(warehouse);
            return Task.CompletedTask;
        }

        public async Task SoftDeleteAsync(int warehouseId, int userId, CancellationToken cancellationToken = default)
        {
            var warehouse = await _context.Warehouses.FirstOrDefaultAsync(w => w.WarehouseID == warehouseId, cancellationToken);
            if (warehouse != null)
            {
                var now = DateTime.UtcNow;
                warehouse.IsDeleted = true;
                warehouse.DeletedAt = now;
                warehouse.UpdatedBy = userId;
                warehouse.UpdatedAt = now;
            }
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
