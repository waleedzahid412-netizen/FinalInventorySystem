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

    /// <summary>

    /// Resolves the ambient company from the <c>wims_company</c> cookie, validates against

    /// non-deleted companies (or the All Companies sentinel), and caches the result in

    /// <see cref="HttpContext.Items"/> once per request.

    /// </summary>

    public sealed class CompanyContext : ICompanyContext

    {

        public const string CookieName = "wims_company";

        public const string AllCompaniesCookieValue = "all";

        private const string ItemsKey = "InventorySystem.CompanyContext.Resolved";



        private readonly IHttpContextAccessor _httpContextAccessor;

        private readonly ApplicationDbContext _db;

        private readonly IUserCompanyAccessService _companyAccess;



        private bool _attempted;

        private bool _isAllCompanies;

        private int? _companyId;

        private string? _companyName;



        public CompanyContext(

            IHttpContextAccessor httpContextAccessor,

            ApplicationDbContext db,

            IUserCompanyAccessService companyAccess)

        {

            _httpContextAccessor = httpContextAccessor;

            _db = db;

            _companyAccess = companyAccess;

        }



        public bool HasCompany => _companyId.HasValue;



        public bool IsAllCompanies => _isAllCompanies;



        public bool HasResolvedScope => _isAllCompanies || _companyId.HasValue;



        public int CompanyID =>

            _companyId ?? throw new InvalidOperationException(

                "No specific company is selected. Resolve a company scope (not All Companies) before reading CompanyID.");



        public string CompanyName =>

            _companyName ?? throw new InvalidOperationException(

                "No specific company is selected. Resolve a company scope (not All Companies) before reading CompanyName.");



        public async Task<bool> TryResolveAsync(CancellationToken cancellationToken = default)

        {

            if (_attempted)

            {

                return HasResolvedScope;

            }



            var httpContext = _httpContextAccessor.HttpContext;

            if (httpContext?.Items[ItemsKey] is ResolvedScope cached)

            {

                ApplyResolved(cached);

                _attempted = true;

                return HasResolvedScope;

            }



            _attempted = true;



            if (httpContext == null)

            {

                return false;

            }



            if (!httpContext.Request.Cookies.TryGetValue(CookieName, out var raw)

                || string.IsNullOrWhiteSpace(raw))

            {

                return false;

            }



            raw = raw.Trim();

            var userId = _companyAccess.GetCurrentUserId();



            if (string.Equals(raw, AllCompaniesCookieValue, StringComparison.OrdinalIgnoreCase))

            {

                if (!userId.HasValue

                    || !await _companyAccess.CanUseAllCompaniesModeAsync(userId.Value, cancellationToken))

                {

                    return false;

                }



                var allScope = ResolvedScope.All();

                ApplyResolved(allScope);

                httpContext.Items[ItemsKey] = allScope;

                return true;

            }



            if (!int.TryParse(raw, out var companyId) || companyId <= 0)

            {

                return false;

            }



            if (userId.HasValue

                && !await _companyAccess.CanAccessCompanyAsync(userId.Value, companyId, cancellationToken))

            {

                return false;

            }



            var company = await _db.Companies

                .AsNoTracking()

                .Where(c => c.CompanyID == companyId)

                .Select(c => new { c.CompanyID, c.CompanyName })

                .FirstOrDefaultAsync(cancellationToken);



            if (company == null)

            {

                return false;

            }



            var specific = ResolvedScope.Specific(company.CompanyID, company.CompanyName);

            ApplyResolved(specific);

            httpContext.Items[ItemsKey] = specific;

            return true;

        }



        private void ApplyResolved(ResolvedScope scope)

        {

            _isAllCompanies = scope.IsAllCompanies;

            _companyId = scope.CompanyID;

            _companyName = scope.CompanyName;

        }



        private sealed record ResolvedScope(bool IsAllCompanies, int? CompanyID, string? CompanyName)

        {

            public static ResolvedScope All() => new(true, null, null);

            public static ResolvedScope Specific(int companyId, string companyName) =>

                new(false, companyId, companyName);

        }

    }

}


