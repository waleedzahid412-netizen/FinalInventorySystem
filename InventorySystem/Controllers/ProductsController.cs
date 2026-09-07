using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using InventorySystem.DTOs.Products;
using InventorySystem.Filters;
using InventorySystem.Helpers;
using InventorySystem.Mappings;
using InventorySystem.Services.Interfaces;
using InventorySystem.Validators.Products;
using InventorySystem.ViewModels.Products;

namespace InventorySystem.Controllers
{
    [Authorize]
    [RequireCompanyScope]
    public class ProductsController : InventoryController
    {
        private readonly IProductService _productService;
        private readonly ILookupService _lookupService;
        private readonly ICompanyContext _companyContext;
        private readonly ILogger<ProductsController> _logger;

        public ProductsController(
            IProductService productService,
            ILookupService lookupService,
            ICompanyContext companyContext,
            ILogger<ProductsController> logger)
        {
            _productService = productService;
            _lookupService = lookupService;
            _companyContext = companyContext;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index([FromQuery] ProductFilterDto filter, CancellationToken cancellationToken)
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

            var pagedProducts = await _productService.GetPagedProductsAsync(filter, cancellationToken);
            var categories = await _lookupService.GetCategoriesAsync(
                _companyContext.HasCompany ? _companyContext.CompanyID : null,
                cancellationToken);

            var viewModel = new ProductListViewModel
            {
                Filter = filter,
                Products = pagedProducts,
                Categories = categories.ToSelectList(filter.CategoryID),
                Companies = Enumerable.Empty<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>(),
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

            var dropdowns = await _lookupService.GetProductFormDropdownsAsync(_companyContext.CompanyID, cancellationToken);

            var viewModel = new CreateProductViewModel
            {
                CompanyID = _companyContext.CompanyID,
                CompanyName = _companyContext.CompanyName,
                Categories = dropdowns.Categories.ToSelectList(),
                AvailableUnits = dropdowns.Units.ToSelectList()
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateProductViewModel model, CancellationToken cancellationToken)
        {
            await _companyContext.TryResolveAsync(cancellationToken);
            var blocked = CompanyScopeGuards.RedirectIfCannotCreate(this, _companyContext);
            if (blocked != null) return blocked;

            model.CompanyID = _companyContext.CompanyID;
            model.CompanyName = _companyContext.CompanyName;

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
            dto.CompanyID = _companyContext.CompanyID;
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

        [HttpGet]
        public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
        {
            await _companyContext.TryResolveAsync(cancellationToken);

            var editDto = await _productService.GetProductForEditAsync(id, cancellationToken);
            if (editDto == null || CompanyScopeGuards.IsOutOfScope(_companyContext, editDto.CompanyID))
            {
                return NotFound();
            }

            var dropdowns = await _lookupService.GetProductFormDropdownsAsync(editDto.CompanyID, cancellationToken);
            var viewModel = editDto.ToViewModel(dropdowns.Categories, dropdowns.Companies, dropdowns.Units);
            viewModel.CompanyName = _companyContext.HasCompany
                ? _companyContext.CompanyName
                : (dropdowns.Companies.FirstOrDefault(c => c.Id == editDto.CompanyID)?.Name ?? CompanyScopeGuards.DisplayName(_companyContext));
            viewModel.Companies = Enumerable.Empty<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>();

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, EditProductViewModel model, CancellationToken cancellationToken)
        {
            if (id != model.ProductID)
            {
                return BadRequest();
            }

            await _companyContext.TryResolveAsync(cancellationToken);

            var existing = await _productService.GetProductForEditAsync(id, cancellationToken);
            if (existing == null || CompanyScopeGuards.IsOutOfScope(_companyContext, existing.CompanyID))
            {
                return NotFound();
            }

            model.CompanyID = existing.CompanyID;
            if (_companyContext.HasCompany)
            {
                model.CompanyName = _companyContext.CompanyName;
            }

            var customErrors = ProductValidationRules.ValidateEditViewModel(model);
            foreach (var err in customErrors)
            {
                ModelState.AddModelError("", err);
            }

            if (!ModelState.IsValid)
            {
                await PopulateFormDropdownsAsync(model, existing.CompanyID, cancellationToken);
                return View(model);
            }

            int currentUserId = GetCurrentUserId();
            var dto = model.ToDto();
            dto.CompanyID = existing.CompanyID; // immutable
            var result = await _productService.UpdateProductAsync(dto, currentUserId, cancellationToken);

            if (!result.Success)
            {
                foreach (var err in result.Errors)
                {
                    ModelState.AddModelError("", err);
                }
                await PopulateFormDropdownsAsync(model, existing.CompanyID, cancellationToken);
                return View(model);
            }

            TempData["SuccessMessage"] = "Product updated successfully!";
            return RedirectToAction(nameof(Details), new { id = model.ProductID });
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id, [FromQuery] string activeTab = "inventory", [FromQuery] int page = 1, CancellationToken cancellationToken = default)
        {
            await _companyContext.TryResolveAsync(cancellationToken);

            var detailsDto = await _productService.GetProductDetailsAsync(id, cancellationToken);
            if (detailsDto == null || CompanyScopeGuards.IsOutOfScope(_companyContext, detailsDto.CompanyID))
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
        {
            await _companyContext.TryResolveAsync(cancellationToken);
            var existing = await _productService.GetProductForEditAsync(id, cancellationToken);
            if (existing == null || CompanyScopeGuards.IsOutOfScope(_companyContext, existing.CompanyID))
            {
                return NotFound();
            }

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

        [HttpGet]
        public async Task<IActionResult> GetCategories(int? companyId, CancellationToken cancellationToken)
        {
            await _companyContext.TryResolveAsync(cancellationToken);
            if (!_companyContext.HasCompany)
            {
                return BadRequest(new { message = CompanyScopeGuards.SelectSpecificCompanyMessage });
            }

            var categories = await _lookupService.GetCategoriesAsync(_companyContext.CompanyID, cancellationToken);
            return Json(categories);
        }

        [HttpGet]
        public async Task<IActionResult> CheckSku([FromQuery] string sku, [FromQuery] int? excludeProductId, CancellationToken cancellationToken)
        {
            bool isUnique = await _productService.ValidateSkuAsync(sku, excludeProductId, cancellationToken);
            return Json(new { isUnique });
        }

        [HttpGet]
        public async Task<IActionResult> CheckBarcode([FromQuery] string barcode, [FromQuery] int? excludeProductId, CancellationToken cancellationToken)
        {
            bool isUnique = await _productService.ValidateBarcodeAsync(barcode, excludeProductId, cancellationToken);
            return Json(new { isUnique });
        }

        private async Task PopulateFormDropdownsAsync(CreateProductViewModel model, CancellationToken cancellationToken)
        {
            var dropdowns = await _lookupService.GetProductFormDropdownsAsync(_companyContext.CompanyID, cancellationToken);
            model.Categories = dropdowns.Categories.ToSelectList(model.CategoryID);
            model.AvailableUnits = dropdowns.Units.ToSelectList(model.BaseUnitID);
            model.CompanyID = _companyContext.CompanyID;
            model.CompanyName = _companyContext.CompanyName;
        }

        private async Task PopulateFormDropdownsAsync(EditProductViewModel model, int companyId, CancellationToken cancellationToken)
        {
            var dropdowns = await _lookupService.GetProductFormDropdownsAsync(companyId, cancellationToken);
            model.Categories = dropdowns.Categories.ToSelectList(model.CategoryID);
            model.AvailableUnits = dropdowns.Units.ToSelectList(model.BaseUnitID);
            model.CompanyID = companyId;
        }
    }
}
