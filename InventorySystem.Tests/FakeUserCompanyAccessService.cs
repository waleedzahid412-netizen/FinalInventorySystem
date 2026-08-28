using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using InventorySystem.Services.Interfaces;

namespace InventorySystem.Tests
{
    public sealed class FakeUserCompanyAccessService : IUserCompanyAccessService
    {
        private readonly int? _userId;
        private readonly bool _unrestricted;

        public FakeUserCompanyAccessService(int? userId = null, bool unrestricted = true)
        {
            _userId = userId;
            _unrestricted = unrestricted;
        }

        public int? GetCurrentUserId() => _userId;

        public Task<bool> IsAdminAsync(int userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_unrestricted);

        public Task<bool> IsCurrentUserAdminAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_unrestricted);

        public Task<UserCompanyAccessProfile> GetAccessProfileAsync(int userId, CancellationToken cancellationToken = default)
        {
            if (_unrestricted)
            {
                return Task.FromResult(new UserCompanyAccessProfile { IsUnrestricted = true });
            }

            return Task.FromResult(new UserCompanyAccessProfile
            {
                IsUnrestricted = false,
                AllowedCompanyIds = new List<int>()
            });
        }

        public Task<bool> CanAccessCompanyAsync(int userId, int companyId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_unrestricted || companyId > 0);

        public Task<bool> CanUseAllCompaniesModeAsync(int userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_unrestricted);
    }
}
