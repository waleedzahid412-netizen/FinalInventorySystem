using Microsoft.AspNetCore.Mvc;
using InventorySystem.Services.Interfaces;

namespace InventorySystem.Helpers
{
    /// <summary>
    /// Shared helpers for ambient company scope on hard-scoped MVC controllers.
    /// </summary>
    public static class CompanyScopeGuards
    {
        public const string SelectSpecificCompanyMessage = "Select a specific company before creating.";

        public const string ManageCompaniesInAllCompaniesMessage =
            "Switch to All Companies to manage the company list.";

        /// <summary>
        /// Company master-data (list, create, edit, delete) is available only in
        /// All Companies mode (or before any scope is chosen). Hidden when a specific company is selected.
        /// </summary>
        public static bool CanManageCompanies(ICompanyContext companyContext) =>
            companyContext.IsAllCompanies || !companyContext.HasResolvedScope;

        /// <summary>
        /// When a specific company is selected, block company-list management screens.
        /// </summary>
        public static IActionResult? RedirectIfCannotManageCompanies(
            Controller controller,
            ICompanyContext companyContext,
            bool notify = true)
        {
            if (CanManageCompanies(companyContext))
            {
                return null;
            }

            if (notify)
            {
                controller.TempData["ErrorMessage"] = ManageCompaniesInAllCompaniesMessage;
            }

            if (companyContext.HasCompany)
            {
                return controller.RedirectToAction("Details", "Companies", new { id = companyContext.CompanyID });
            }

            return controller.RedirectToAction("Index", "Home");
        }

        /// <summary>
        /// True when returnUrl points at company master-data screens that are invalid
        /// under a specific-company scope.
        /// </summary>
        public static bool IsCompanyManagementPath(string? returnUrl)
        {
            if (string.IsNullOrWhiteSpace(returnUrl))
            {
                return false;
            }

            var path = returnUrl.Split('?', '#')[0].TrimEnd('/');
            return path.Equals("/Companies", System.StringComparison.OrdinalIgnoreCase)
                   || path.StartsWith("/Companies/Index", System.StringComparison.OrdinalIgnoreCase)
                   || path.StartsWith("/Companies/Create", System.StringComparison.OrdinalIgnoreCase)
                   || path.StartsWith("/Companies/Edit", System.StringComparison.OrdinalIgnoreCase)
                   || path.StartsWith("/Companies/Delete", System.StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Blocks Create when ambient scope is All Companies or otherwise not a specific company.
        /// Caller must have already resolved the scope (e.g. via <see cref="Filters.RequireCompanyScopeAttribute"/>).
        /// </summary>
        public static IActionResult? RedirectIfCannotCreate(Controller controller, ICompanyContext companyContext, string indexAction = "Index")
        {
            if (companyContext.IsAllCompanies || !companyContext.HasCompany)
            {
                controller.TempData["ErrorMessage"] = SelectSpecificCompanyMessage;
                return controller.RedirectToAction(indexAction);
            }

            return null;
        }

        /// <summary>
        /// True when a specific company is selected and the entity belongs to a different company.
        /// Skipped (returns false) in All Companies mode.
        /// </summary>
        public static bool IsOutOfScope(ICompanyContext companyContext, int entityCompanyId) =>
            companyContext.HasCompany && entityCompanyId != companyContext.CompanyID;

        /// <summary>
        /// Display label for list headers: company name when specific, otherwise "All Companies".
        /// </summary>
        public static string DisplayName(ICompanyContext companyContext) =>
            companyContext.HasCompany ? companyContext.CompanyName : "All Companies";
    }
}
