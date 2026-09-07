using System;

using System.Linq;

using System.Threading;

using System.Threading.Tasks;

using Microsoft.AspNetCore.Authorization;

using Microsoft.AspNetCore.Mvc;

using InventorySystem.Authorization;

using InventorySystem.Helpers;

using InventorySystem.Services.Implementations;

using InventorySystem.Services.Interfaces;



namespace InventorySystem.Controllers

{

    [Authorize]

    [SkipPermissionCheck]

    public class CompanyScopeController : Controller

    {

        /// <summary>Posted companyId for ambient All Companies (not a real Company row).</summary>

        public const int AllCompaniesCompanyId = 0;



        private readonly ILookupService _lookupService;

        private readonly IUserCompanyAccessService _companyAccess;

        private readonly ICompanyScopeCookieService _cookieService;



        public CompanyScopeController(

            ILookupService lookupService,

            IUserCompanyAccessService companyAccess,

            ICompanyScopeCookieService cookieService)

        {

            _lookupService = lookupService;

            _companyAccess = companyAccess;

            _cookieService = cookieService;

        }



        /// <summary>

        /// Legacy deep-link fallback. Routine switching uses the navbar dropdown.

        /// </summary>

        [HttpGet]

        public IActionResult Select(string? returnUrl)

        {

            TempData["ToastError"] = "Use the company dropdown in the top bar to switch companies.";

            var safeReturn = SanitizeReturnUrl(returnUrl);

            return LocalRedirect(safeReturn);

        }



        /// <summary>

        /// Sets ambient scope. <paramref name="companyId"/> = positive ID for a company,

        /// or <see cref="AllCompaniesCompanyId"/> (0) for All Companies.

        /// </summary>

        [HttpPost]

        [ValidateAntiForgeryToken]

        public async Task<IActionResult> Switch(int companyId, string? returnUrl, CancellationToken cancellationToken)

        {

            var safeReturn = SanitizeReturnUrl(returnUrl);

            bool switchingToSpecific = companyId != AllCompaniesCompanyId;



            if (companyId == AllCompaniesCompanyId)

            {

                var userId = _companyAccess.GetCurrentUserId()

                    ?? throw new MissingUserIdentityException();

                if (!await _companyAccess.CanUseAllCompaniesModeAsync(userId, cancellationToken))

                {

                    TempData["ToastError"] = "You do not have permission to use All Companies mode.";

                    return LocalRedirect(safeReturn);

                }



                _cookieService.SetScopeCookie(Response, Request, CompanyContext.AllCompaniesCookieValue);

                return LocalRedirect(safeReturn);

            }



            var companies = await _lookupService.GetCompaniesAsync(cancellationToken);

            var selected = companies.FirstOrDefault(c => c.Id == companyId);

            if (selected == null)

            {

                TempData["ToastError"] = "That company is not available. Choose another.";

                return LocalRedirect(safeReturn);

            }



            _cookieService.SetScopeCookie(Response, Request, companyId.ToString());



            if (switchingToSpecific && CompanyScopeGuards.IsCompanyManagementPath(safeReturn))

            {

                return RedirectToAction("Index", "Home");

            }



            return LocalRedirect(safeReturn);

        }



        private string SanitizeReturnUrl(string? returnUrl)

        {

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))

            {

                return returnUrl;

            }



            return Url.Action("Index", "Home") ?? "/";

        }

    }

}


