using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using InventorySystem.Data;
using InventorySystem.DTOs.Areas;
using InventorySystem.DTOs.Common;
using InventorySystem.Models.Entities;
using InventorySystem.Repositories.Interfaces;

namespace InventorySystem.Repositories.Implementations
{
    public class AreaRepository : IAreaRepository
    {
        private readonly ApplicationDbContext _context;

        public AreaRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<PagedResult<Area>> GetPagedAsync(AreaFilterDto filter, CancellationToken cancellationToken = default)
        {
            var query = _context.Areas.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                var term = filter.SearchTerm.Trim();
                query = query.Where(a => a.AreaName.Contains(term));
            }

            if (filter.IsActive.HasValue)
            {
                query = query.Where(a => a.IsActive == filter.IsActive.Value);
            }

            query = filter.SortBy?.ToLower() switch
            {
                "status" => filter.IsAscending
                    ? query.OrderBy(a => a.IsActive).ThenBy(a => a.AreaName)
                    : query.OrderByDescending(a => a.IsActive).ThenBy(a => a.AreaName),
                "createdat" => filter.IsAscending
                    ? query.OrderBy(a => a.CreatedAt)
                    : query.OrderByDescending(a => a.CreatedAt),
                _ => filter.IsAscending
                    ? query.OrderBy(a => a.AreaName)
                    : query.OrderByDescending(a => a.AreaName)
            };

            int totalCount = await query.CountAsync(cancellationToken);
            var items = await query
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync(cancellationToken);

            return new PagedResult<Area>(items, totalCount, filter.PageNumber, filter.PageSize);
        }

        public async Task<Area?> GetByIdAsync(int areaId, CancellationToken cancellationToken = default)
        {
            return await _context.Areas.FirstOrDefaultAsync(a => a.AreaID == areaId, cancellationToken);
        }

        public async Task<IReadOnlyList<Area>> GetActiveAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Areas
                .AsNoTracking()
                .Where(a => a.IsActive)
                .OrderBy(a => a.AreaName)
                .ToListAsync(cancellationToken);
        }

        public async Task<bool> ExistsByNameAsync(string name, int? excludeAreaId = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            var normalized = name.Trim().ToLower();
            var query = _context.Areas.AsNoTracking().Where(a => a.AreaName.ToLower() == normalized);
            if (excludeAreaId.HasValue)
            {
                query = query.Where(a => a.AreaID != excludeAreaId.Value);
            }
            return await query.AnyAsync(cancellationToken);
        }

        public async Task<bool> IsInUseAsync(int areaId, CancellationToken cancellationToken = default)
        {
            if (await _context.SubAreas.AnyAsync(s => s.AreaID == areaId, cancellationToken))
                return true;

            if (await _context.Customers.AnyAsync(c => c.AreaID == areaId, cancellationToken))
                return true;

            return await _context.SalesInvoices.AnyAsync(si => si.AreaID == areaId, cancellationToken);
        }

        public async Task<int> CountSubAreasAsync(int areaId, CancellationToken cancellationToken = default)
        {
            return await _context.SubAreas.AsNoTracking().CountAsync(s => s.AreaID == areaId, cancellationToken);
        }

        public async Task AddAsync(Area area, CancellationToken cancellationToken = default)
        {
            await _context.Areas.AddAsync(area, cancellationToken);
        }

        public Task UpdateAsync(Area area, CancellationToken cancellationToken = default)
        {
            var entry = _context.Entry(area);
            if (entry.State == EntityState.Detached)
            {
                _context.Areas.Update(area);
            }
            return Task.CompletedTask;
        }

        public async Task SoftDeleteAsync(int areaId, int userId, CancellationToken cancellationToken = default)
        {
            var area = await _context.Areas.FirstOrDefaultAsync(a => a.AreaID == areaId, cancellationToken);
            if (area == null) return;

            var now = DateTime.UtcNow;
            area.IsDeleted = true;
            area.DeletedAt = now;
            area.IsActive = false;
            area.UpdatedAt = now;
            area.UpdatedBy = userId;
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
