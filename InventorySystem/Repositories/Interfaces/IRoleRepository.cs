using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using InventorySystem.Models.Entities;

namespace InventorySystem.Repositories.Interfaces
{
    public interface IRoleRepository
    {
        Task<List<Role>> GetAllActiveAsync(CancellationToken cancellationToken = default);
        Task<Role?> GetByIdAsync(int roleId, CancellationToken cancellationToken = default);
        Task<Role?> GetByIdWithPermissionsAsync(int roleId, CancellationToken cancellationToken = default);
        Task<bool> RoleNameExistsAsync(string roleName, int? excludeRoleId = null, CancellationToken cancellationToken = default);
        Task<int> GetActiveUserCountAsync(int roleId, CancellationToken cancellationToken = default);
        Task<List<ApplicationModule>> GetPermissionCatalogAsync(CancellationToken cancellationToken = default);
        Task AddAsync(Role role, CancellationToken cancellationToken = default);
        Task SavePermissionsAsync(int roleId, IEnumerable<RolePermission> permissions, CancellationToken cancellationToken = default);
        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
