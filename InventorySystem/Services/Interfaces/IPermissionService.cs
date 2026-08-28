using System.Threading;
using System.Threading.Tasks;
using InventorySystem.Constants;
using InventorySystem.Models.Entities;

namespace InventorySystem.Services.Interfaces
{
    public interface IPermissionService
    {
        Task<bool> HasPermissionAsync(int userId, string pageKey, PermissionAction action, CancellationToken cancellationToken = default);
        Task<bool> HasPermissionAsync(string? roleName, int roleId, string pageKey, PermissionAction action, CancellationToken cancellationToken = default);
        Task<ApplicationPage?> ResolvePageAsync(string controllerName, string actionName, CancellationToken cancellationToken = default);
        PermissionAction MapMvcActionToPermission(string actionName);
        void InvalidateRoleCache(int roleId);
    }
}
