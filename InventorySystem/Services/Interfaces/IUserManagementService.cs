using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using InventorySystem.DTOs.Common;
using InventorySystem.DTOs.UserManagement;

namespace InventorySystem.Services.Interfaces
{
    public interface IUserManagementService
    {
        Task<PagedResult<UserListItemDto>> GetPagedUsersAsync(UserFilterDto filter, CancellationToken cancellationToken = default);
        Task<UserEditDto?> GetUserForEditAsync(int userId, CancellationToken cancellationToken = default);
        Task<List<AssignableRoleDto>> GetAssignableRolesForActorAsync(int actorUserId, CancellationToken cancellationToken = default);
        Task<OperationResult> CreateUserAsync(CreateUserDto dto, int actorUserId, CancellationToken cancellationToken = default);
        Task<OperationResult> UpdateUserAsync(UpdateUserDto dto, int actorUserId, CancellationToken cancellationToken = default);
        Task<OperationResult> SoftDeleteUserAsync(int userId, int actorUserId, CancellationToken cancellationToken = default);
    }
}
