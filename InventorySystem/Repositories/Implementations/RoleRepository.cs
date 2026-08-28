using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using InventorySystem.Data;
using InventorySystem.Models.Entities;
using InventorySystem.Repositories.Interfaces;

namespace InventorySystem.Repositories.Implementations
{
    public class RoleRepository : IRoleRepository
    {
        private readonly ApplicationDbContext _context;

        public RoleRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<Role>> GetAllActiveAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Roles
                .AsNoTracking()
                .Include(r => r.RolePermissions)
                .OrderBy(r => r.RoleName)
                .ToListAsync(cancellationToken);
        }

        public async Task<Role?> GetByIdAsync(int roleId, CancellationToken cancellationToken = default)
        {
            return await _context.Roles
                .FirstOrDefaultAsync(r => r.RoleID == roleId, cancellationToken);
        }

        public async Task<Role?> GetByIdWithPermissionsAsync(int roleId, CancellationToken cancellationToken = default)
        {
            return await _context.Roles
                .Include(r => r.RolePermissions)
                .FirstOrDefaultAsync(r => r.RoleID == roleId, cancellationToken);
        }

        public async Task<bool> RoleNameExistsAsync(string roleName, int? excludeRoleId = null, CancellationToken cancellationToken = default)
        {
            var normalized = roleName.Trim().ToLowerInvariant();
            var query = _context.Roles
                .AsNoTracking()
                .Where(r => r.RoleName.ToLower() == normalized);

            if (excludeRoleId.HasValue)
            {
                query = query.Where(r => r.RoleID != excludeRoleId.Value);
            }

            return await query.AnyAsync(cancellationToken);
        }

        public async Task<int> GetActiveUserCountAsync(int roleId, CancellationToken cancellationToken = default)
        {
            return await _context.Users
                .AsNoTracking()
                .CountAsync(u => u.RoleID == roleId && u.IsActive, cancellationToken);
        }

        public async Task<List<ApplicationModule>> GetPermissionCatalogAsync(CancellationToken cancellationToken = default)
        {
            return await _context.ApplicationModules
                .AsNoTracking()
                .Where(m => m.IsActive)
                .Include(m => m.Pages.Where(p => p.IsActive))
                .OrderBy(m => m.DisplayOrder)
                .ToListAsync(cancellationToken);
        }

        public async Task AddAsync(Role role, CancellationToken cancellationToken = default)
        {
            await _context.Roles.AddAsync(role, cancellationToken);
        }

        public async Task SavePermissionsAsync(int roleId, IEnumerable<RolePermission> permissions, CancellationToken cancellationToken = default)
        {
            var existing = await _context.RolePermissions
                .Where(rp => rp.RoleID == roleId)
                .ToListAsync(cancellationToken);

            _context.RolePermissions.RemoveRange(existing);
            await _context.RolePermissions.AddRangeAsync(permissions, cancellationToken);
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
