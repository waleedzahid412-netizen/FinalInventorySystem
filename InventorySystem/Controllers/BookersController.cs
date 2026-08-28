using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InventorySystem.DTOs.Bookers;
using InventorySystem.Filters;
using InventorySystem.Helpers;
using InventorySystem.Mappings;
using InventorySystem.Services.Interfaces;
using InventorySystem.ViewModels.Bookers;

namespace InventorySystem.Controllers
{
    [Authorize]
    [RequireCompanyScope]
    public class BookersController : Controller
    {
        private readonly IBookerService _bookerService;
        private readonly ICompanyContext _companyContext;

        public BookersController(IBookerService bookerService, ICompanyContext companyContext)
        {
            _bookerService = bookerService;
            _companyContext = companyContext;
        }

        [HttpGet]
        public async Task<IActionResult> Index([FromQuery] BookerFilterDto filter, CancellationToken cancellationToken)
        {
            await _companyContext.TryResolveAsync(cancellationToken);
            // Filtering by company is applied inside BookerService (HasCompany vs All).
            var paged = await _bookerService.GetPagedBookersAsync(filter, cancellationToken);
            return View(new BookerListViewModel { Filter = filter, Bookers = paged });
        }

        [HttpGet]
        public async Task<IActionResult> Create(CancellationToken cancellationToken)
        {
            await _companyContext.TryResolveAsync(cancellationToken);
            var blocked = CompanyScopeGuards.RedirectIfCannotCreate(this, _companyContext);
            if (blocked != null) return blocked;

            return View(new CreateBookerViewModel
            {
                CompanyName = _companyContext.CompanyName
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateBookerViewModel model, CancellationToken cancellationToken)
        {
            await _companyContext.TryResolveAsync(cancellationToken);
            var blocked = CompanyScopeGuards.RedirectIfCannotCreate(this, _companyContext);
            if (blocked != null) return blocked;

            model.CompanyName = _companyContext.CompanyName;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var result = await _bookerService.CreateBookerAsync(model.ToDto(), GetCurrentUserId(), cancellationToken);
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

        [HttpGet]
        public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
        {
            await _companyContext.TryResolveAsync(cancellationToken);

            var dto = await _bookerService.GetBookerForEditAsync(id, cancellationToken);
            if (dto == null || CompanyScopeGuards.IsOutOfScope(_companyContext, dto.CompanyID))
            {
                TempData["ErrorMessage"] = "Booker not found.";
                return RedirectToAction(nameof(Index));
            }

            return View(dto.ToViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, EditBookerViewModel model, CancellationToken cancellationToken)
        {
            if (id != model.BookerID)
            {
                return BadRequest();
            }

            await _companyContext.TryResolveAsync(cancellationToken);

            var existing = await _bookerService.GetBookerForEditAsync(id, cancellationToken);
            if (existing == null || CompanyScopeGuards.IsOutOfScope(_companyContext, existing.CompanyID))
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var result = await _bookerService.UpdateBookerAsync(model.ToDto(), GetCurrentUserId(), cancellationToken);
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
        {
            await _companyContext.TryResolveAsync(cancellationToken);

            var existing = await _bookerService.GetBookerForEditAsync(id, cancellationToken);
            if (existing == null || CompanyScopeGuards.IsOutOfScope(_companyContext, existing.CompanyID))
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = "Booker not found." });
                }
                return NotFound();
            }

            var result = await _bookerService.SoftDeleteBookerAsync(id, GetCurrentUserId(), cancellationToken);

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
                TempData["ErrorMessage"] = result.Errors.FirstOrDefault() ?? "Failed to delete booker.";
            }

            return RedirectToAction(nameof(Index));
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (claim != null && int.TryParse(claim.Value, out int userId))
            {
                return userId;
            }
            return 0;
        }
    }
}
