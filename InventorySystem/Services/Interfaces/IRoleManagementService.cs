using System.Threading;
using System.Threading.Tasks;
using InventorySystem.DTOs.Common;
using InventorySystem.DTOs.Roles;

namespace InventorySystem.Services.Interfaces
{
    public interface IRoleManagementService
    {
        Task<List<RoleListItemDto>> GetAllRolesAsync(CancellationToken cancellationToken = default);
        Task<EditRoleDto?> GetRoleForEditAsync(int roleId, CancellationToken cancellationToken = default);
        Task<List<RolePermissionCatalogModuleDto>> GetEmptyPermissionCatalogAsync(CancellationToken cancellationToken = default);
        Task<OperationResult<int>> CreateRoleAsync(CreateRoleDto dto, int userId, CancellationToken cancellationToken = default);
        Task<OperationResult> UpdateRoleAsync(EditRoleDto dto, int userId, CancellationToken cancellationToken = default);
        Task<OperationResult> SoftDeleteRoleAsync(int roleId, int userId, CancellationToken cancellationToken = default);
    }
}
