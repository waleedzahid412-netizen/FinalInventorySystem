using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using InventorySystem.DTOs.Categories;
using InventorySystem.Filters;
using InventorySystem.Helpers;
using InventorySystem.Mappings;
using InventorySystem.Services.Interfaces;
using InventorySystem.ViewModels.Categories;

namespace InventorySystem.Controllers
{
    [Authorize]
    [RequireCompanyScope]
    public class CategoriesController : InventoryController
    {
        private readonly ICategoryService _categoryService;
        private readonly ILookupService _lookupService;
        private readonly ICompanyContext _companyContext;
        private readonly ILogger<CategoriesController> _logger;

        public CategoriesController(
            ICategoryService categoryService,
            ILookupService lookupService,
            ICompanyContext companyContext,
            ILogger<CategoriesController> logger)
        {
            _categoryService = categoryService;
            _lookupService = lookupService;
            _companyContext = companyContext;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index([FromQuery] CategoryFilterDto filter, CancellationToken cancellationToken)
        {
            await _companyContext.TryResolveAsync(cancellationToken);

            if (_companyContext.HasCompany)
            {
                filter.CompanyID = _companyContext.CompanyID;
            }
            else
            {
                filter.CompanyID = null;
            }

            var pagedCategories = await _categoryService.GetPagedCategoriesAsync(filter, cancellationToken);

            var viewModel = new CategoryListViewModel
            {
                Filter = filter,
                Categories = pagedCategories,
                Companies = new(),
                CompanyName = CompanyScopeGuards.DisplayName(_companyContext)
            };

            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> Create(CancellationToken cancellationToken)
        {
            await _companyContext.TryResolveAsync(cancellationToken);
            var blocked = CompanyScopeGuards.RedirectIfCannotCreate(this, _companyContext);
            if (blocked != null) return blocked;

            return View(new CreateCategoryViewModel
            {
                CompanyID = _companyContext.CompanyID,
                CompanyName = _companyContext.CompanyName
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateCategoryViewModel model, CancellationToken cancellationToken)
        {
            await _companyContext.TryResolveAsync(cancellationToken);
            var blocked = CompanyScopeGuards.RedirectIfCannotCreate(this, _companyContext);
            if (blocked != null) return blocked;

            model.CompanyID = _companyContext.CompanyID;
            model.CompanyName = _companyContext.CompanyName;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            int userId = GetCurrentUserId();
            var dto = model.ToDto();
            dto.CompanyID = _companyContext.CompanyID;
            var result = await _categoryService.CreateCategoryAsync(dto, userId, cancellationToken);

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

            var editDto = await _categoryService.GetCategoryForEditAsync(id, cancellationToken);
            if (editDto == null || CompanyScopeGuards.IsOutOfScope(_companyContext, editDto.CompanyID))
            {
                TempData["ErrorMessage"] = "Category not found.";
                return RedirectToAction(nameof(Index));
            }

            bool hasProducts = await _categoryService.HasProductsAsync(id, cancellationToken);
            var model = editDto.ToViewModel(hasProducts);
            if (_companyContext.HasCompany)
            {
                model.CompanyName = _companyContext.CompanyName;
            }
            else
            {
                var companies = await _lookupService.GetCompaniesAsync(cancellationToken);
                model.CompanyName = companies.FirstOrDefault(c => c.Id == editDto.CompanyID)?.Name
                    ?? CompanyScopeGuards.DisplayName(_companyContext);
            }
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, EditCategoryViewModel model, CancellationToken cancellationToken)
        {
            if (id != model.CategoryID)
            {
                return BadRequest();
            }

            await _companyContext.TryResolveAsync(cancellationToken);

            var existing = await _categoryService.GetCategoryForEditAsync(id, cancellationToken);
            if (existing == null || CompanyScopeGuards.IsOutOfScope(_companyContext, existing.CompanyID))
            {
                return NotFound();
            }

            model.CompanyID = existing.CompanyID;
            if (_companyContext.HasCompany)
            {
                model.CompanyName = _companyContext.CompanyName;
            }
            bool hasProducts = await _categoryService.HasProductsAsync(id, cancellationToken);
            model.HasProducts = hasProducts;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            int userId = GetCurrentUserId();
            var dto = model.ToDto();
            dto.CompanyID = existing.CompanyID;
            var result = await _categoryService.UpdateCategoryAsync(dto, userId, cancellationToken);

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
            var existing = await _categoryService.GetCategoryForEditAsync(id, cancellationToken);
            if (existing == null || CompanyScopeGuards.IsOutOfScope(_companyContext, existing.CompanyID))
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = "Category not found." });
                }
                return NotFound();
            }

            int userId = GetCurrentUserId();
            var result = await _categoryService.SoftDeleteCategoryAsync(id, userId, cancellationToken);

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
                TempData["ErrorMessage"] = result.Errors.FirstOrDefault() ?? "Failed to delete category.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
