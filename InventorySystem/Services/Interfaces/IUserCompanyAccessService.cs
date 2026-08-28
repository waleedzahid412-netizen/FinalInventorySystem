using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace InventorySystem.Services.Interfaces
{
    public sealed class UserCompanyAccessProfile
    {
        public bool IsUnrestricted { get; init; }
        public IReadOnlyList<int> AllowedCompanyIds { get; init; } = new List<int>();
    }

    public interface IUserCompanyAccessService
    {
        int? GetCurrentUserId();
        Task<bool> IsAdminAsync(int userId, CancellationToken cancellationToken = default);
        Task<bool> IsCurrentUserAdminAsync(CancellationToken cancellationToken = default);
        Task<UserCompanyAccessProfile> GetAccessProfileAsync(int userId, CancellationToken cancellationToken = default);
        Task<bool> CanAccessCompanyAsync(int userId, int companyId, CancellationToken cancellationToken = default);
        Task<bool> CanUseAllCompaniesModeAsync(int userId, CancellationToken cancellationToken = default);
    }
}
