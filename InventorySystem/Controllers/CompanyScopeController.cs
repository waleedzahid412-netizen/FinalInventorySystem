using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using InventorySystem.Helpers;
using InventorySystem.Services.Implementations;
using InventorySystem.Services.Interfaces;
using InventorySystem.ViewModels.CompanyScope;

namespace InventorySystem.Controllers
{
    [Authorize]
    public class CompanyScopeController : Controller
    {
        /// <summary>Posted companyId for ambient All Companies (not a real Company row).</summary>
        public const int AllCompaniesCompanyId = 0;

        private readonly ILookupService _lookupService;
        private readonly ICompanyContext _companyContext;
        private readonly IUserCompanyAccessService _companyAccess;

        public CompanyScopeController(
            ILookupService lookupService,
            ICompanyContext companyContext,
            IUserCompanyAccessService companyAccess)
        {
            _lookupService = lookupService;
            _companyContext = companyContext;
            _companyAccess = companyAccess;
        }

        [HttpGet]
        public async Task<IActionResult> Select(string? returnUrl, CancellationToken cancellationToken)
        {
            var companies = await _lookupService.GetCompaniesAsync(cancellationToken);
            await _companyContext.TryResolveAsync(cancellationToken);

            var model = new CompanyScopeSelectViewModel
            {
                ReturnUrl = SanitizeReturnUrl(returnUrl),
                Companies = companies
                    .Select(c => new CompanyScopeOptionViewModel
                    {
                        CompanyID = c.Id,
                        CompanyName = c.Name,
                        IsCurrent = _companyContext.HasCompany && _companyContext.CompanyID == c.Id
                    })
                    .ToList()
            };

            return View(model);
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
                var userId = _companyAccess.GetCurrentUserId();
                if (!userId.HasValue
                    || !await _companyAccess.CanUseAllCompaniesModeAsync(userId.Value, cancellationToken))
                {
                    TempData["ErrorMessage"] = "You do not have permission to use All Companies mode.";
                    return RedirectToAction(nameof(Select), new { returnUrl = safeReturn });
                }

                AppendScopeCookie(CompanyContext.AllCompaniesCookieValue);
                return LocalRedirect(safeReturn);
            }

            var companies = await _lookupService.GetCompaniesAsync(cancellationToken);
            var selected = companies.FirstOrDefault(c => c.Id == companyId);
            if (selected == null)
            {
                TempData["ErrorMessage"] = "That company is not available. Choose another.";
                return RedirectToAction(nameof(Select), new { returnUrl = safeReturn });
            }

            AppendScopeCookie(companyId.ToString());

            // Company list/create/edit are All-Companies-only — don't bounce back there after picking a company.
            if (switchingToSpecific && CompanyScopeGuards.IsCompanyManagementPath(safeReturn))
            {
                return RedirectToAction("Index", "Home");
            }

            return LocalRedirect(safeReturn);
        }

        private void AppendScopeCookie(string value)
        {
            Response.Cookies.Append(
                CompanyContext.CookieName,
                value,
                new CookieOptions
                {
                    HttpOnly = true,
                    IsEssential = true,
                    SameSite = SameSiteMode.Lax,
                    Secure = Request.IsHttps,
                    Expires = DateTimeOffset.UtcNow.AddDays(30),
                    Path = "/"
                });
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
