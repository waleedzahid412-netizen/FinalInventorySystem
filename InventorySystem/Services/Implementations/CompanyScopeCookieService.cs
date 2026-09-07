using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using InventorySystem.Data;
using InventorySystem.Services.Interfaces;

namespace InventorySystem.Services.Implementations
{
    public sealed class CompanyScopeCookieService : ICompanyScopeCookieService
    {
        private readonly ApplicationDbContext _db;
        private readonly IUserCompanyAccessService _companyAccess;
        private readonly ICompanyContext _companyContext;

        public CompanyScopeCookieService(
            ApplicationDbContext db,
            IUserCompanyAccessService companyAccess,
            ICompanyContext companyContext)
        {
            _db = db;
            _companyAccess = companyAccess;
            _companyContext = companyContext;
        }

        public void SetScopeCookie(HttpResponse response, HttpRequest request, string value)
        {
            response.Cookies.Append(
                CompanyContext.CookieName,
                value,
                new CookieOptions
                {
                    HttpOnly = true,
                    IsEssential = true,
                    SameSite = SameSiteMode.Lax,
                    Secure = request.IsHttps,
                    Expires = DateTimeOffset.UtcNow.AddDays(30),
                    Path = "/"
                });
        }

        public async Task<int?> GetDefaultCompanyIdForUserAsync(int userId, CancellationToken cancellationToken = default)
        {
            var profile = await _companyAccess.GetAccessProfileAsync(userId, cancellationToken);

            var query = _db.Companies
                .AsNoTracking()
                .Where(c => !c.IsDeleted);

            if (!profile.IsUnrestricted)
            {
                if (profile.AllowedCompanyIds.Count == 0)
                {
                    return null;
                }

                query = query.Where(c => profile.AllowedCompanyIds.Contains(c.CompanyID));
            }

            return await query
                .OrderBy(c => c.CompanyName)
                .Select(c => (int?)c.CompanyID)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<bool> TryEnsureDefaultCompanyAsync(
            HttpContext httpContext,
            int userId,
            CancellationToken cancellationToken = default)
        {
            if (await _companyContext.TryResolveAsync(cancellationToken) && _companyContext.HasResolvedScope)
            {
                return true;
            }

            var defaultCompanyId = await GetDefaultCompanyIdForUserAsync(userId, cancellationToken);
            if (!defaultCompanyId.HasValue)
            {
                return false;
            }

            var company = await _db.Companies
                .AsNoTracking()
                .Where(c => c.CompanyID == defaultCompanyId.Value && !c.IsDeleted)
                .Select(c => new { c.CompanyID, c.CompanyName })
                .FirstOrDefaultAsync(cancellationToken);

            if (company == null)
            {
                return false;
            }

            SetScopeCookie(httpContext.Response, httpContext.Request, company.CompanyID.ToString());
            CompanyContext.SetRequestScope(httpContext, company.CompanyID, company.CompanyName);
            _companyContext.ReapplyFromHttpContext();
            return true;
        }
    }
}
