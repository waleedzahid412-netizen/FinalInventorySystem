using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using InventorySystem.Constants;

namespace InventorySystem.Services.Interfaces
{
    public interface IUserPermissionContext
    {
        Task EnsureLoadedAsync(CancellationToken cancellationToken = default);
        bool IsAdmin { get; }
        bool CanView(string pageKey);
        bool CanAdd(string pageKey);
        bool CanEdit(string pageKey);
        bool CanDelete(string pageKey);
    }
}
