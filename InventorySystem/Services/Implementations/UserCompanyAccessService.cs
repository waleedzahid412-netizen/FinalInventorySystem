using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using InventorySystem.Constants;
using InventorySystem.Data;
using InventorySystem.Helpers;
using InventorySystem.Services.Interfaces;

namespace InventorySystem.Services.Implementations
{
    public sealed class UserCompanyAccessService : IUserCompanyAccessService
    {
        private const string ProfileCacheKeyPrefix = "InventorySystem.UserCompanyAccess.Profile";

        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ApplicationDbContext _db;

        public UserCompanyAccessService(IHttpContextAccessor httpContextAccessor, ApplicationDbContext db)
        {
            _httpContextAccessor = httpContextAccessor;
            _db = db;
        }

        public int? GetCurrentUserId()
        {
            return CurrentUserHelper.TryGetUserId(_httpContextAccessor.HttpContext?.User, out var userId)
                ? userId
                : null;
        }

        public async Task<bool> IsCurrentUserAdminAsync(CancellationToken cancellationToken = default)
        {
            var userId = GetCurrentUserId();
            return userId.HasValue && await IsAdminAsync(userId.Value, cancellationToken);
        }

        public async Task<bool> IsAdminAsync(int userId, CancellationToken cancellationToken = default)
        {
            var profile = await GetAccessProfileAsync(userId, cancellationToken);
            return profile.IsUnrestricted;
        }

        public async Task<UserCompanyAccessProfile> GetAccessProfileAsync(int userId, CancellationToken cancellationToken = default)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var cacheKey = $"{ProfileCacheKeyPrefix}:{userId}";
            if (httpContext?.Items[cacheKey] is UserCompanyAccessProfile cached)
            {
                return cached;
            }

            var roleName = await _db.Users
                .AsNoTracking()
                .Where(u => u.UserID == userId && u.IsActive)
                .Select(u => u.Role.RoleName)
                .FirstOrDefaultAsync(cancellationToken);

            UserCompanyAccessProfile profile;
            if (string.Equals(roleName, RoleNames.Admin, StringComparison.OrdinalIgnoreCase))
            {
                profile = new UserCompanyAccessProfile { IsUnrestricted = true };
            }
            else
            {
                var companyIds = await _db.UserCompanies
                    .AsNoTracking()
                    .Where(uc => uc.UserID == userId)
                    .Join(
                        _db.Companies.AsNoTracking().Where(c => !c.IsDeleted),
                        uc => uc.CompanyID,
                        c => c.CompanyID,
                        (uc, c) => c.CompanyID)
                    .Distinct()
                    .OrderBy(id => id)
                    .ToListAsync(cancellationToken);

                profile = new UserCompanyAccessProfile
                {
                    IsUnrestricted = false,
                    AllowedCompanyIds = companyIds
                };
            }

            if (httpContext != null)
            {
                httpContext.Items[cacheKey] = profile;
            }

            return profile;
        }

        public async Task<bool> CanAccessCompanyAsync(int userId, int companyId, CancellationToken cancellationToken = default)
        {
            if (companyId <= 0)
            {
                return false;
            }

            var profile = await GetAccessProfileAsync(userId, cancellationToken);
            if (profile.IsUnrestricted)
            {
                return await _db.Companies
                    .AsNoTracking()
                    .AnyAsync(c => c.CompanyID == companyId && !c.IsDeleted, cancellationToken);
            }

            return profile.AllowedCompanyIds.Contains(companyId);
        }

        public async Task<bool> CanUseAllCompaniesModeAsync(int userId, CancellationToken cancellationToken = default)
        {
            var profile = await GetAccessProfileAsync(userId, cancellationToken);
            return profile.IsUnrestricted;
        }
    }
}
