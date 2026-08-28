using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Logging;
using InventorySystem.DTOs.Purchases;
using InventorySystem.Filters;
using InventorySystem.Helpers;
using InventorySystem.Mappings;
using InventorySystem.Services.Interfaces;
using InventorySystem.ViewModels.Purchases;

namespace InventorySystem.Controllers
{
    [Authorize]
    [RequireCompanyScope]
    public class PurchasesController : Controller
    {
        private readonly IPurchaseService _purchaseService;
        private readonly ILookupService _lookupService;
        private readonly IPdfService _pdfService;
        private readonly ICompanyContext _companyContext;
        private readonly ILogger<PurchasesController> _logger;

        public PurchasesController(
            IPurchaseService purchaseService,
            ILookupService lookupService,
            IPdfService pdfService,
            ICompanyContext companyContext,
            ILogger<PurchasesController> logger)
        {
            _purchaseService = purchaseService;
            _lookupService = lookupService;
            _pdfService = pdfService;
            _companyContext = companyContext;
            _logger = logger;
        }

        // GET: Purchases
        [HttpGet]
        public async Task<IActionResult> Index([FromQuery] PurchaseFilterViewModel filter, CancellationToken cancellationToken)
        {
            await _companyContext.TryResolveAsync(cancellationToken);
            filter ??= new PurchaseFilterViewModel();

            if (_companyContext.HasCompany)
            {
                filter.CompanyID = _companyContext.CompanyID;
            }
            else
            {
                filter.CompanyID = null;
            }

            var filterDto = new PurchaseFilterDto
            {
                InvoiceNumber = filter.InvoiceNumber,
                CompanyID = filter.CompanyID,
                WarehouseID = filter.WarehouseID,
                DateFrom = filter.DateFrom,
                DateTo = filter.DateTo,
                PaymentStatus = filter.PaymentStatus,
                PageNumber = filter.PageNumber < 1 ? 1 : filter.PageNumber,
                PageSize = filter.PageSize < 1 ? 10 : filter.PageSize
            };

            var pagedPurchases = await _purchaseService.GetPagedPurchasesAsync(filterDto, cancellationToken);
            var warehouses = await _lookupService.GetWarehousesAsync(cancellationToken);

            var viewModel = new PurchaseListViewModel
            {
                Filter = filter,
                Items = pagedPurchases,
                Companies = new SelectList(Enumerable.Empty<SelectListItem>()),
                Warehouses = new SelectList(warehouses, "Id", "Name", filter.WarehouseID),
                CompanyName = CompanyScopeGuards.DisplayName(_companyContext)
            };

            return View(viewModel);
        }

        // GET: Purchases/Create
        [HttpGet]
        public async Task<IActionResult> Create(CancellationToken cancellationToken)
        {
            await _companyContext.TryResolveAsync(cancellationToken);
            var blocked = CompanyScopeGuards.RedirectIfCannotCreate(this, _companyContext);
            if (blocked != null) return blocked;

            var mainWarehouseId = await _lookupService.GetMainWarehouseIdAsync(cancellationToken);
            var viewModel = new CreatePurchaseViewModel
            {
                InvoiceDate = DateTime.Today,
                CompanyID = _companyContext.CompanyID,
                CompanyName = _companyContext.CompanyName,
                WarehouseID = mainWarehouseId ?? 0
            };

            await PopulateDropdownsAsync(viewModel, cancellationToken);
            return View(viewModel);
        }

        // POST: Purchases/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreatePurchaseViewModel viewModel, CancellationToken cancellationToken)
        {
            await _companyContext.TryResolveAsync(cancellationToken);
            var blocked = CompanyScopeGuards.RedirectIfCannotCreate(this, _companyContext);
            if (blocked != null) return blocked;

            viewModel.CompanyID = _companyContext.CompanyID;
            viewModel.CompanyName = _companyContext.CompanyName;

            if (!ModelState.IsValid)
            {
                await PopulateDropdownsAsync(viewModel, cancellationToken);
                return View(viewModel);
            }

            var dto = viewModel.ToDto();
            dto.CompanyID = _companyContext.CompanyID;
            int userId = GetCurrentUserId();

            var result = await _purchaseService.CreateAndFinalizePurchaseInvoiceAsync(dto, userId, cancellationToken);
            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.Message);
                await PopulateDropdownsAsync(viewModel, cancellationToken);
                return View(viewModel);
            }

            TempData["SuccessMessage"] = "Purchase Invoice finalized successfully. Stock and Company Ledger updated.";
            return RedirectToAction(nameof(Details), new { id = result.Data });
        }

        // GET: Purchases/Details/5
        [HttpGet]
        public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
        {
            await _companyContext.TryResolveAsync(cancellationToken);

            var detailsDto = await _purchaseService.GetPurchaseDetailsAsync(id, cancellationToken);
            if (detailsDto == null)
            {
                TempData["ErrorMessage"] = "Purchase invoice not found.";
                return RedirectToAction(nameof(Index));
            }

            if (CompanyScopeGuards.IsOutOfScope(_companyContext, detailsDto.Header.CompanyID))
            {
                return NotFound();
            }

            var viewModel = detailsDto.ToViewModel();
            return View(viewModel);
        }

        // GET: Purchases/PaymentHistoryTab/5?pageNumber=1
        [HttpGet]
        public async Task<IActionResult> PaymentHistoryTab(int id, int pageNumber = 1, CancellationToken cancellationToken = default)
        {
            await _companyContext.TryResolveAsync(cancellationToken);
            var details = await _purchaseService.GetPurchaseDetailsAsync(id, cancellationToken);
            if (details == null || CompanyScopeGuards.IsOutOfScope(_companyContext, details.Header.CompanyID))
            {
                return NotFound();
            }

            var history = await _purchaseService.GetPaymentHistoryTabAsync(id, pageNumber, 10, cancellationToken);
            return PartialView("_PaymentHistoryTab", history);
        }

        // GET: Purchases/LedgerTab/5?pageNumber=1
        [HttpGet]
        public async Task<IActionResult> LedgerTab(int id, int pageNumber = 1, CancellationToken cancellationToken = default)
        {
            await _companyContext.TryResolveAsync(cancellationToken);
            var details = await _purchaseService.GetPurchaseDetailsAsync(id, cancellationToken);
            if (details == null || CompanyScopeGuards.IsOutOfScope(_companyContext, details.Header.CompanyID))
            {
                return NotFound();
            }

            var ledger = await _purchaseService.GetLedgerTabAsync(id, pageNumber, 10, cancellationToken);
            return PartialView("_LedgerTab", ledger);
        }

        // ===== AJAX LOOKUP ENDPOINTS =====

        [HttpGet]
        public async Task<IActionResult> GetProductsByCompany(int companyId, CancellationToken cancellationToken)
        {
            await _companyContext.TryResolveAsync(cancellationToken);

            // Ignore query companyId — use ambient scope only.
            if (!_companyContext.HasCompany)
            {
                return BadRequest(new { message = CompanyScopeGuards.SelectSpecificCompanyMessage });
            }

            var products = await _lookupService.GetProductsByCompanyAsync(_companyContext.CompanyID, cancellationToken);
            return Json(products);
        }

        [HttpGet]
        public async Task<IActionResult> GetProductUnits(int productId, CancellationToken cancellationToken)
        {
            var units = await _lookupService.GetProductUnitsAsync(productId, cancellationToken);
            var result = units.Select(u => new
            {
                id = u.ProductUnitID,
                productUnitId = u.ProductUnitID,
                name = u.UnitName,
                unitName = u.UnitName,
                purchasePrice = u.PurchasePrice ?? 0m,
                conversionFactor = u.ConversionToBaseUnit,
                conversionToBaseUnit = u.ConversionToBaseUnit
            });
            return Json(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetProductInfo(int productId, CancellationToken cancellationToken)
        {
            var info = await _lookupService.GetProductInfoAsync(productId, cancellationToken);
            return Json(info);
        }

        [HttpGet]
        public async Task<IActionResult> DownloadPdf(int id, CancellationToken cancellationToken)
        {
            await _companyContext.TryResolveAsync(cancellationToken);

            var details = await _purchaseService.GetPurchaseDetailsAsync(id, cancellationToken);
            if (details == null || CompanyScopeGuards.IsOutOfScope(_companyContext, details.Header.CompanyID))
            {
                return NotFound();
            }

            var pdfBytes = _pdfService.GeneratePurchaseInvoicePdf(details);
            return File(pdfBytes, "application/pdf", $"PurchaseInvoice_{details.Header.InvoiceNumber}.pdf");
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
        {
            await _companyContext.TryResolveAsync(cancellationToken);

            var checkResult = await _purchaseService.CanEditPurchaseInvoiceAsync(id, cancellationToken);
            if (!checkResult.Success)
            {
                TempData["ErrorMessage"] = checkResult.Message;
                return RedirectToAction("Details", new { id });
            }

            var details = await _purchaseService.GetPurchaseDetailsAsync(id, cancellationToken);
            if (details == null || CompanyScopeGuards.IsOutOfScope(_companyContext, details.Header.CompanyID))
            {
                return NotFound();
            }

            var initialItems = details.Items.Select(i => new CreatePurchaseItemDto
            {
                ProductID = i.ProductID,
                ProductUnitID = i.ProductUnitID,
                Quantity = i.Quantity,
                UnitCost = i.UnitCost
            }).ToList();

            var vm = new EditPurchaseViewModel
            {
                PurchaseInvoiceID = details.Header.PurchaseInvoiceID,
                InvoiceNumber = details.Header.InvoiceNumber,
                CompanyID = details.Header.CompanyID,
                CompanyName = details.Header.CompanyName,
                WarehouseID = details.Header.WarehouseID,
                WarehouseName = details.Header.WarehouseName,
                InvoiceDate = details.Header.InvoiceDate,
                ItemsJson = JsonSerializer.Serialize(initialItems)
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditPurchaseViewModel model, CancellationToken cancellationToken)
        {
            await _companyContext.TryResolveAsync(cancellationToken);

            var existing = await _purchaseService.GetPurchaseDetailsAsync(model.PurchaseInvoiceID, cancellationToken);
            if (existing == null || CompanyScopeGuards.IsOutOfScope(_companyContext, existing.Header.CompanyID))
            {
                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(model.EditReason))
            {
                ModelState.AddModelError("EditReason", "An edit reason is required.");
            }

            List<CreatePurchaseItemDto> items = new List<CreatePurchaseItemDto>();
            try
            {
                items = JsonSerializer.Deserialize<List<CreatePurchaseItemDto>>(model.ItemsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<CreatePurchaseItemDto>();
            }
            catch (Exception)
            {
                ModelState.AddModelError("ItemsJson", "Invalid line items format.");
            }

            if (!items.Any())
            {
                ModelState.AddModelError("ItemsJson", "At least one valid line item is required.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var updateDto = new UpdatePurchaseInvoiceDto
            {
                PurchaseInvoiceID = model.PurchaseInvoiceID,
                InvoiceDate = model.InvoiceDate,
                Notes = model.Notes,
                EditReason = model.EditReason,
                Items = items
            };

            int userId = GetCurrentUserId();
            var result = await _purchaseService.UpdatePurchaseInvoiceAsync(updateDto, userId, cancellationToken);

            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.Message);
                return View(model);
            }

            TempData["SuccessMessage"] = result.Message;
            return RedirectToAction("Details", new { id = model.PurchaseInvoiceID });
        }

        private async Task PopulateDropdownsAsync(CreatePurchaseViewModel model, CancellationToken cancellationToken)
        {
            var warehouses = await _lookupService.GetWarehousesAsync(cancellationToken);

            model.CompanyID = _companyContext.CompanyID;
            model.CompanyName = _companyContext.CompanyName;
            model.Companies = new SelectList(Enumerable.Empty<SelectListItem>());
            model.Warehouses = new SelectList(warehouses, "Id", "Name", model.WarehouseID);
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (claim != null && int.TryParse(claim.Value, out int userId))
            {
                return userId;
            }
            return 1; // Default Admin fallback
        }
    }
}
