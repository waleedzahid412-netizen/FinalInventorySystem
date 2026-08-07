using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Logging;
using InventorySystem.DTOs.Products;
using InventorySystem.Mappings;
using InventorySystem.Services.Interfaces;
using InventorySystem.Validators.Products;
using InventorySystem.ViewModels.Products;

namespace InventorySystem.Controllers
{
    [Authorize]
    public class ProductsController : Controller
    {
        private readonly IProductService _productService;
        private readonly ILookupService _lookupService;
        private readonly ILogger<ProductsController> _logger;

        public ProductsController(
            IProductService productService,
            ILookupService lookupService,
            ILogger<ProductsController> logger)
        {
            _productService = productService;
            _lookupService = lookupService;
            _logger = logger;
        }

        // GET: Products
        [HttpGet]
        public async Task<IActionResult> Index([FromQuery] ProductFilterDto filter, CancellationToken cancellationToken)
        {
            var pagedProducts = await _productService.GetPagedProductsAsync(filter, cancellationToken);
            var categories = await _lookupService.GetCategoriesAsync(cancellationToken);
            var companies = await _lookupService.GetCompaniesAsync(cancellationToken);

            var viewModel = new ProductListViewModel
            {
                Filter = filter,
                Products = pagedProducts,
                Categories = categories.ToSelectList(filter.CategoryID),
                Companies = companies.ToSelectList(filter.CompanyID)
            };

            return View(viewModel);
        }

        // GET: Products/Create
        [HttpGet]
        public async Task<IActionResult> Create(CancellationToken cancellationToken)
        {
            var dropdowns = await _lookupService.GetProductFormDropdownsAsync(cancellationToken);

            var viewModel = new CreateProductViewModel
            {
                Categories = dropdowns.Categories.ToSelectList(),
                Companies = dropdowns.Companies.ToSelectList(),
                AvailableUnits = dropdowns.Units.ToSelectList()
            };

            return View(viewModel);
        }

        // POST: Products/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateProductViewModel model, CancellationToken cancellationToken)
        {
            var customErrors = ProductValidationRules.ValidateCreateViewModel(model);
            foreach (var err in customErrors)
            {
                ModelState.AddModelError("", err);
            }

            if (!ModelState.IsValid)
            {
                await PopulateFormDropdownsAsync(model, cancellationToken);
                return View(model);
            }

            int currentUserId = GetCurrentUserId();
            var dto = model.ToDto();
            var result = await _productService.CreateProductAsync(dto, currentUserId, cancellationToken);

            if (!result.Success)
            {
                foreach (var err in result.Errors)
                {
                    ModelState.AddModelError("", err);
                }
                await PopulateFormDropdownsAsync(model, cancellationToken);
                return View(model);
            }

            TempData["SuccessMessage"] = "Product created successfully!";
            return RedirectToAction(nameof(Details), new { id = result.Data });
        }

        // GET: Products/Edit/5
        [HttpGet]
        public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
        {
            var editDto = await _productService.GetProductForEditAsync(id, cancellationToken);
            if (editDto == null)
            {
                return NotFound();
            }

            var dropdowns = await _lookupService.GetProductFormDropdownsAsync(cancellationToken);
            var viewModel = editDto.ToViewModel(dropdowns.Categories, dropdowns.Companies, dropdowns.Units);

            return View(viewModel);
        }

        // POST: Products/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, EditProductViewModel model, CancellationToken cancellationToken)
        {
            if (id != model.ProductID)
            {
                return BadRequest();
            }

            var customErrors = ProductValidationRules.ValidateEditViewModel(model);
            foreach (var err in customErrors)
            {
                ModelState.AddModelError("", err);
            }

            if (!ModelState.IsValid)
            {
                await PopulateFormDropdownsAsync(model, cancellationToken);
                return View(model);
            }

            int currentUserId = GetCurrentUserId();
            var dto = model.ToDto();
            var result = await _productService.UpdateProductAsync(dto, currentUserId, cancellationToken);

            if (!result.Success)
            {
                foreach (var err in result.Errors)
                {
                    ModelState.AddModelError("", err);
                }
                await PopulateFormDropdownsAsync(model, cancellationToken);
                return View(model);
            }

            TempData["SuccessMessage"] = "Product updated successfully!";
            return RedirectToAction(nameof(Details), new { id = model.ProductID });
        }

        // GET: Products/Details/5
        [HttpGet]
        public async Task<IActionResult> Details(int id, [FromQuery] string activeTab = "inventory", [FromQuery] int page = 1, CancellationToken cancellationToken = default)
        {
            var detailsDto = await _productService.GetProductDetailsAsync(id, cancellationToken);
            if (detailsDto == null)
            {
                return NotFound();
            }

            var viewModel = detailsDto.ToViewModel();
            viewModel.ActiveTab = string.IsNullOrWhiteSpace(activeTab) ? "inventory" : activeTab.ToLower();

            int pageSize = 10;
            switch (viewModel.ActiveTab)
            {
                case "purchase":
                    viewModel.PurchaseHistory = await _productService.GetPurchaseHistoryAsync(id, page, pageSize, cancellationToken);
                    break;
                case "sales":
                    viewModel.SalesHistory = await _productService.GetSalesHistoryAsync(id, page, pageSize, cancellationToken);
                    break;
                case "transactions":
                    viewModel.InventoryTransactions = await _productService.GetInventoryTransactionsAsync(id, page, pageSize, cancellationToken);
                    break;
            }

            return View(viewModel);
        }

        // POST: Products/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
        {
            int currentUserId = GetCurrentUserId();
            var result = await _productService.SoftDeleteProductAsync(id, currentUserId, cancellationToken);

            if (!result.Success)
            {
                TempData["ErrorMessage"] = result.Message;
            }
            else
            {
                TempData["SuccessMessage"] = "Product deleted successfully.";
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Products/CheckSku
        [HttpGet]
        public async Task<IActionResult> CheckSku([FromQuery] string sku, [FromQuery] int? excludeProductId, CancellationToken cancellationToken)
        {
            bool isUnique = await _productService.ValidateSkuAsync(sku, excludeProductId, cancellationToken);
            return Json(new { isUnique });
        }

        // GET: Products/CheckBarcode
        [HttpGet]
        public async Task<IActionResult> CheckBarcode([FromQuery] string barcode, [FromQuery] int? excludeProductId, CancellationToken cancellationToken)
        {
            bool isUnique = await _productService.ValidateBarcodeAsync(barcode, excludeProductId, cancellationToken);
            return Json(new { isUnique });
        }

        private async Task PopulateFormDropdownsAsync(CreateProductViewModel model, CancellationToken cancellationToken)
        {
            var dropdowns = await _lookupService.GetProductFormDropdownsAsync(cancellationToken);
            model.Categories = dropdowns.Categories.ToSelectList(model.CategoryID);
            model.Companies = dropdowns.Companies.ToSelectList(model.CompanyID);
            model.AvailableUnits = dropdowns.Units.ToSelectList(model.BaseUnitID);
        }

        private async Task PopulateFormDropdownsAsync(EditProductViewModel model, CancellationToken cancellationToken)
        {
            var dropdowns = await _lookupService.GetProductFormDropdownsAsync(cancellationToken);
            model.Categories = dropdowns.Categories.ToSelectList(model.CategoryID);
            model.Companies = dropdowns.Companies.ToSelectList(model.CompanyID);
            model.AvailableUnits = dropdowns.Units.ToSelectList(model.BaseUnitID);
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (claim != null && int.TryParse(claim.Value, out int userId))
            {
                return userId;
            }
            return 1; // Default System/Admin User ID fallback
        }
    }
}
