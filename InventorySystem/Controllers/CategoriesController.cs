using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Logging;
using InventorySystem.DTOs.Categories;
using InventorySystem.Mappings;
using InventorySystem.Services.Interfaces;
using InventorySystem.ViewModels.Categories;

namespace InventorySystem.Controllers
{
    [Authorize]
    public class CategoriesController : Controller
    {
        private readonly ICategoryService _categoryService;
        private readonly ILookupService _lookupService;
        private readonly ILogger<CategoriesController> _logger;

        public CategoriesController(
            ICategoryService categoryService,
            ILookupService lookupService,
            ILogger<CategoriesController> logger)
        {
            _categoryService = categoryService;
            _lookupService = lookupService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index([FromQuery] CategoryFilterDto filter, CancellationToken cancellationToken)
        {
            var pagedCategories = await _categoryService.GetPagedCategoriesAsync(filter, cancellationToken);
            var companies = await _lookupService.GetCompaniesAsync(cancellationToken);

            var viewModel = new CategoryListViewModel
            {
                Filter = filter,
                Categories = pagedCategories,
                Companies = companies.Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = c.Name,
                    Selected = c.Id == filter.CompanyID
                }).ToList()
            };

            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> Create(CancellationToken cancellationToken)
        {
            var model = new CreateCategoryViewModel();
            await PopulateCompaniesAsync(model, cancellationToken);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateCategoryViewModel model, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                await PopulateCompaniesAsync(model, cancellationToken);
                return View(model);
            }

            int userId = GetCurrentUserId();
            var result = await _categoryService.CreateCategoryAsync(model.ToDto(), userId, cancellationToken);

            if (!result.Success)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error);
                }
                await PopulateCompaniesAsync(model, cancellationToken);
                return View(model);
            }

            TempData["SuccessMessage"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
        {
            var editDto = await _categoryService.GetCategoryForEditAsync(id, cancellationToken);
            if (editDto == null)
            {
                TempData["ErrorMessage"] = "Category not found.";
                return RedirectToAction(nameof(Index));
            }

            bool hasProducts = await _categoryService.HasProductsAsync(id, cancellationToken);
            var model = editDto.ToViewModel(hasProducts);
            await PopulateCompaniesAsync(model, cancellationToken);
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

            bool hasProducts = await _categoryService.HasProductsAsync(id, cancellationToken);
            model.HasProducts = hasProducts;

            if (!ModelState.IsValid)
            {
                await PopulateCompaniesAsync(model, cancellationToken);
                return View(model);
            }

            int userId = GetCurrentUserId();
            var result = await _categoryService.UpdateCategoryAsync(model.ToDto(), userId, cancellationToken);

            if (!result.Success)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error);
                }
                await PopulateCompaniesAsync(model, cancellationToken);
                return View(model);
            }

            TempData["SuccessMessage"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
        {
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

        private async Task PopulateCompaniesAsync(CreateCategoryViewModel model, CancellationToken cancellationToken)
        {
            var companies = await _lookupService.GetCompaniesAsync(cancellationToken);
            model.Companies = companies.Select(c => new SelectListItem
            {
                Value = c.Id.ToString(),
                Text = c.Name,
                Selected = c.Id == model.CompanyID
            }).ToList();
        }

        private async Task PopulateCompaniesAsync(EditCategoryViewModel model, CancellationToken cancellationToken)
        {
            var companies = await _lookupService.GetCompaniesAsync(cancellationToken);
            model.Companies = companies.Select(c => new SelectListItem
            {
                Value = c.Id.ToString(),
                Text = c.Name,
                Selected = c.Id == model.CompanyID
            }).ToList();
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (claim != null && int.TryParse(claim.Value, out int userId))
            {
                return userId;
            }
            return 1;
        }
    }
}
