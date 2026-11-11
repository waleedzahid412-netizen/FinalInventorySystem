using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using InventorySystem.Data;
using InventorySystem.DTOs.Common;
using InventorySystem.DTOs.SubAreas;
using InventorySystem.Models.Entities;
using InventorySystem.Repositories.Interfaces;

namespace InventorySystem.Repositories.Implementations
{
    public class SubAreaRepository : ISubAreaRepository
    {
        private readonly ApplicationDbContext _context;

        public SubAreaRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<PagedResult<SubArea>> GetPagedAsync(SubAreaFilterDto filter, CancellationToken cancellationToken = default)
        {
            var query = _context.SubAreas
                .AsNoTracking()
                .Include(s => s.Area)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                var term = filter.SearchTerm.Trim();
                query = query.Where(s =>
                    s.SubAreaName.Contains(term) ||
                    s.Area.AreaName.Contains(term));
            }

            if (filter.AreaID.HasValue && filter.AreaID.Value > 0)
            {
                query = query.Where(s => s.AreaID == filter.AreaID.Value);
            }

            if (filter.IsActive.HasValue)
            {
                query = query.Where(s => s.IsActive == filter.IsActive.Value);
            }

            query = filter.SortBy?.ToLower() switch
            {
                "area" => filter.IsAscending
                    ? query.OrderBy(s => s.Area.AreaName).ThenBy(s => s.SubAreaName)
                    : query.OrderByDescending(s => s.Area.AreaName).ThenBy(s => s.SubAreaName),
                "status" => filter.IsAscending
                    ? query.OrderBy(s => s.IsActive).ThenBy(s => s.SubAreaName)
                    : query.OrderByDescending(s => s.IsActive).ThenBy(s => s.SubAreaName),
                "createdat" => filter.IsAscending
                    ? query.OrderBy(s => s.CreatedAt)
                    : query.OrderByDescending(s => s.CreatedAt),
                _ => filter.IsAscending
                    ? query.OrderBy(s => s.SubAreaName)
                    : query.OrderByDescending(s => s.SubAreaName)
            };

            int totalCount = await query.CountAsync(cancellationToken);
            var items = await query
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync(cancellationToken);

            return new PagedResult<SubArea>(items, totalCount, filter.PageNumber, filter.PageSize);
        }

        public async Task<SubArea?> GetByIdAsync(int subAreaId, CancellationToken cancellationToken = default)
        {
            return await _context.SubAreas
                .Include(s => s.Area)
                .FirstOrDefaultAsync(s => s.SubAreaID == subAreaId, cancellationToken);
        }

        public async Task<bool> ExistsByNameAsync(int areaId, string name, int? excludeSubAreaId = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            var normalized = name.Trim().ToLower();
            var query = _context.SubAreas.AsNoTracking()
                .Where(s => s.AreaID == areaId && s.SubAreaName.ToLower() == normalized);
            if (excludeSubAreaId.HasValue)
            {
                query = query.Where(s => s.SubAreaID != excludeSubAreaId.Value);
            }
            return await query.AnyAsync(cancellationToken);
        }

        public async Task<bool> AreaExistsAsync(int areaId, CancellationToken cancellationToken = default)
        {
            return await _context.Areas.AsNoTracking().AnyAsync(a => a.AreaID == areaId, cancellationToken);
        }

        public async Task<bool> IsInUseAsync(int subAreaId, CancellationToken cancellationToken = default)
        {
            if (await _context.Customers.AnyAsync(c => c.SubAreaID == subAreaId, cancellationToken))
                return true;

            return await _context.SalesInvoices.AnyAsync(si => si.SubAreaID == subAreaId, cancellationToken);
        }

        public async Task AddAsync(SubArea subArea, CancellationToken cancellationToken = default)
        {
            await _context.SubAreas.AddAsync(subArea, cancellationToken);
        }

        public Task UpdateAsync(SubArea subArea, CancellationToken cancellationToken = default)
        {
            var entry = _context.Entry(subArea);
            if (entry.State == EntityState.Detached)
            {
                _context.SubAreas.Update(subArea);
            }
            return Task.CompletedTask;
        }

        public async Task SoftDeleteAsync(int subAreaId, int userId, CancellationToken cancellationToken = default)
        {
            var subArea = await _context.SubAreas.FirstOrDefaultAsync(s => s.SubAreaID == subAreaId, cancellationToken);
            if (subArea == null) return;

            var now = DateTime.UtcNow;
            subArea.IsDeleted = true;
            subArea.DeletedAt = now;
            subArea.IsActive = false;
            subArea.UpdatedAt = now;
            subArea.UpdatedBy = userId;
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
