using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using InventorySystem.DTOs.Companies;
using InventorySystem.Helpers;
using InventorySystem.Mappings;
using InventorySystem.Services.Interfaces;
using InventorySystem.ViewModels.Companies;

namespace InventorySystem.Controllers
{
    [Authorize]
    public class CompaniesController : Controller
    {
        private readonly ICompanyService _companyService;
        private readonly ICompanyContext _companyContext;
        private readonly ILogger<CompaniesController> _logger;

        public CompaniesController(
            ICompanyService companyService,
            ICompanyContext companyContext,
            ILogger<CompaniesController> logger)
        {
            _companyService = companyService;
            _companyContext = companyContext;
            _logger = logger;
        }

        // GET: Companies
        [HttpGet]
        public async Task<IActionResult> Index([FromQuery] CompanyFilterDto filter, CancellationToken cancellationToken)
        {
            await _companyContext.TryResolveAsync(cancellationToken);
            var scopedAway = CompanyScopeGuards.RedirectIfCannotManageCompanies(this, _companyContext, notify: true);
            if (scopedAway != null)
            {
                return scopedAway;
            }

            var pagedCompanies = await _companyService.GetPagedCompaniesAsync(filter, cancellationToken);

            var viewModel = new CompanyListViewModel
            {
                Filter = filter,
                Companies = pagedCompanies
            };

            return View(viewModel);
        }

        // GET: Companies/Create
        [HttpGet]
        public async Task<IActionResult> Create(CancellationToken cancellationToken)
        {
            await _companyContext.TryResolveAsync(cancellationToken);
            var blocked = CompanyScopeGuards.RedirectIfCannotManageCompanies(this, _companyContext);
            if (blocked != null)
            {
                return blocked;
            }

            return View(new CreateCompanyViewModel());
        }

        // POST: Companies/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateCompanyViewModel model, CancellationToken cancellationToken)
        {
            await _companyContext.TryResolveAsync(cancellationToken);
            var blocked = CompanyScopeGuards.RedirectIfCannotManageCompanies(this, _companyContext);
            if (blocked != null)
            {
                return blocked;
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            int userId = GetCurrentUserId();
            var result = await _companyService.CreateCompanyAsync(model.ToDto(), userId, cancellationToken);

            if (!result.Success)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error);
                }
                return View(model);
            }

            TempData["SuccessMessage"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        // GET: Companies/Edit/5
        [HttpGet]
        public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
        {
            await _companyContext.TryResolveAsync(cancellationToken);
            var blocked = CompanyScopeGuards.RedirectIfCannotManageCompanies(this, _companyContext);
            if (blocked != null)
            {
                return blocked;
            }

            var editDto = await _companyService.GetCompanyForEditAsync(id, cancellationToken);
            if (editDto == null)
            {
                TempData["ErrorMessage"] = "Company not found.";
                return RedirectToAction(nameof(Index));
            }

            return View(editDto.ToViewModel());
        }

        // POST: Companies/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, EditCompanyViewModel model, CancellationToken cancellationToken)
        {
            await _companyContext.TryResolveAsync(cancellationToken);
            var blocked = CompanyScopeGuards.RedirectIfCannotManageCompanies(this, _companyContext);
            if (blocked != null)
            {
                return blocked;
            }

            if (id != model.CompanyID)
            {
                return BadRequest();
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            int userId = GetCurrentUserId();
            var result = await _companyService.UpdateCompanyAsync(model.ToDto(), userId, cancellationToken);

            if (!result.Success)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error);
                }
                return View(model);
            }

            TempData["SuccessMessage"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        // GET: Companies/Details/5
        [HttpGet]
        public async Task<IActionResult> Details(
            int id, 
            [FromQuery] string activeTab = "purchases", 
            [FromQuery] int pageNumber = 1, 
            [FromQuery] int pageSize = 10, 
            CancellationToken cancellationToken = default)
        {
            await _companyContext.TryResolveAsync(cancellationToken);
            if (_companyContext.HasCompany && id != _companyContext.CompanyID)
            {
                TempData["ErrorMessage"] = CompanyScopeGuards.ManageCompaniesInAllCompaniesMessage;
                return RedirectToAction(nameof(Details), new { id = _companyContext.CompanyID });
            }

            var detailsDto = await _companyService.GetCompanyDetailsAsync(id, cancellationToken);
            if (detailsDto == null)
            {
                TempData["ErrorMessage"] = "Company not found.";
                return _companyContext.HasCompany
                    ? RedirectToAction("Index", "Home")
                    : RedirectToAction(nameof(Index));
            }

            var viewModel = detailsDto.ToViewModel();
            viewModel.ActiveTab = activeTab.ToLower();

            switch (viewModel.ActiveTab)
            {
                case "payments":
                    viewModel.PaymentHistory = await _companyService.GetPaymentHistoryAsync(id, pageNumber, pageSize, cancellationToken);
                    break;
                case "ledger":
                    viewModel.CompanyLedger = await _companyService.GetLedgerAsync(id, pageNumber, pageSize, cancellationToken);
                    break;
                case "purchases":
                default:
                    viewModel.PurchaseHistory = await _companyService.GetPurchaseHistoryAsync(id, pageNumber, pageSize, cancellationToken);
                    break;
            }

            return View(viewModel);
        }

        // POST: Companies/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
        {
            await _companyContext.TryResolveAsync(cancellationToken);
            if (_companyContext.HasCompany)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = CompanyScopeGuards.ManageCompaniesInAllCompaniesMessage });
                }

                var blocked = CompanyScopeGuards.RedirectIfCannotManageCompanies(this, _companyContext);
                if (blocked != null)
                {
                    return blocked;
                }
            }

            int userId = GetCurrentUserId();
            var result = await _companyService.SoftDeleteCompanyAsync(id, userId, cancellationToken);

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new { success = result.Success, message = result.Success ? result.Message : result.Errors.FirstOrDefault() });
            }

            if (result.Success)
            {
                TempData["SuccessMessage"] = result.Message;
            }
            else
            {
                TempData["ErrorMessage"] = result.Errors.FirstOrDefault() ?? "Failed to delete company.";
            }

            return RedirectToAction(nameof(Index));
        }

        // AJAX Validation
        [HttpGet]
        public async Task<IActionResult> CheckName(string name, int? excludeCompanyId, CancellationToken cancellationToken)
        {
            bool isUnique = await _companyService.ValidateCompanyNameAsync(name, excludeCompanyId, cancellationToken);
            return Json(isUnique);
        }

        [HttpGet]
        public async Task<IActionResult> CheckPhone(string phone, int? excludeCompanyId, CancellationToken cancellationToken)
        {
            bool isUnique = await _companyService.ValidatePhoneAsync(phone, excludeCompanyId, cancellationToken);
            return Json(isUnique);
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (claim != null && int.TryParse(claim.Value, out int userId))
            {
                return userId;
            }
            return 1; // Default fallback to system admin
        }
    }
}
