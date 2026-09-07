using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using InventorySystem.Data;
using InventorySystem.DTOs.Common;
using InventorySystem.DTOs.Units;
using InventorySystem.Models.Entities;
using InventorySystem.Repositories.Interfaces;

namespace InventorySystem.Repositories.Implementations
{
    public class UnitRepository : IUnitRepository
    {
        private readonly ApplicationDbContext _context;

        public UnitRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<PagedResult<Unit>> GetPagedAsync(UnitFilterDto filter, CancellationToken cancellationToken = default)
        {
            var query = _context.Units.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                var term = filter.SearchTerm.Trim();
                query = query.Where(u => u.UnitName.Contains(term));
            }

            if (filter.IsActive.HasValue)
            {
                query = query.Where(u => u.IsActive == filter.IsActive.Value);
            }

            query = filter.SortBy?.ToLower() switch
            {
                "createdat" => filter.IsAscending
                    ? query.OrderBy(u => u.CreatedAt)
                    : query.OrderByDescending(u => u.CreatedAt),
                "status" => filter.IsAscending
                    ? query.OrderBy(u => u.IsActive).ThenBy(u => u.UnitName)
                    : query.OrderByDescending(u => u.IsActive).ThenBy(u => u.UnitName),
                _ => filter.IsAscending
                    ? query.OrderBy(u => u.UnitName)
                    : query.OrderByDescending(u => u.UnitName)
            };

            int totalCount = await query.CountAsync(cancellationToken);

            var items = await query
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync(cancellationToken);

            return new PagedResult<Unit>(items, totalCount, filter.PageNumber, filter.PageSize);
        }

        public async Task<Unit?> GetByIdAsync(int unitId, CancellationToken cancellationToken = default)
        {
            return await _context.Units
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UnitID == unitId, cancellationToken);
        }

        public async Task<bool> ExistsByNameAsync(string unitName, int? excludeUnitId = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(unitName)) return false;
            var normalizedName = unitName.Trim().ToLower();

            var query = _context.Units
                .AsNoTracking()
                .Where(u => u.UnitName.ToLower() == normalizedName);

            if (excludeUnitId.HasValue)
            {
                query = query.Where(u => u.UnitID != excludeUnitId.Value);
            }

            return await query.AnyAsync(cancellationToken);
        }

        public async Task<bool> IsInUseAsync(int unitId, CancellationToken cancellationToken = default)
        {
            bool usedAsBase = await _context.Products.AnyAsync(p => p.BaseUnitID == unitId, cancellationToken);
            if (usedAsBase) return true;

            return await _context.ProductUnits.AnyAsync(pu => pu.UnitID == unitId, cancellationToken);
        }

        public async Task AddAsync(Unit unit, CancellationToken cancellationToken = default)
        {
            await _context.Units.AddAsync(unit, cancellationToken);
        }

        public Task UpdateAsync(Unit unit, CancellationToken cancellationToken = default)
        {
            _context.Units.Update(unit);
            return Task.CompletedTask;
        }

        public async Task SoftDeleteAsync(int unitId, int userId, CancellationToken cancellationToken = default)
        {
            var unit = await _context.Units.FirstOrDefaultAsync(u => u.UnitID == unitId, cancellationToken);
            if (unit != null)
            {
                var now = DateTime.UtcNow;
                unit.IsDeleted = true;
                unit.DeletedAt = now;
                unit.UpdatedBy = userId;
                unit.UpdatedAt = now;
            }
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
