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
using Microsoft.EntityFrameworkCore;
using InventorySystem.DTOs.Sales;
using InventorySystem.Filters;
using InventorySystem.Helpers;
using InventorySystem.Mappings;
using InventorySystem.Services.Interfaces;
using InventorySystem.ViewModels.Sales;

namespace InventorySystem.Controllers
{
    [Authorize]
    [RequireCompanyScope]
    public class SalesController : Controller
    {
        private readonly ISalesService _salesService;
        private readonly ILookupService _lookupService;
        private readonly IPromotionDiscountService _promotionDiscountService;
        private readonly IPdfService _pdfService;
        private readonly ICompanyContext _companyContext;
        private readonly InventorySystem.Data.ApplicationDbContext _context;
        private readonly Microsoft.Extensions.Logging.ILogger<SalesController> _logger;

        public SalesController(
            ISalesService salesService,
            ILookupService lookupService,
            IPromotionDiscountService promotionDiscountService,
            IPdfService pdfService,
            ICompanyContext companyContext,
            InventorySystem.Data.ApplicationDbContext context,
            Microsoft.Extensions.Logging.ILogger<SalesController> logger)
        {
            _salesService = salesService;
            _lookupService = lookupService;
            _promotionDiscountService = promotionDiscountService;
            _pdfService = pdfService;
            _companyContext = companyContext;
            _context = context;
            _logger = logger;
        }

        // GET: Sales
        [HttpGet]
        public async Task<IActionResult> Index(SalesFilterViewModel filter, CancellationToken cancellationToken)
        {
            await _companyContext.TryResolveAsync(cancellationToken);
            filter ??= new SalesFilterViewModel();

            // Specific company: force ambient scope (ignore client). All Companies: leave null (show all).
            if (_companyContext.HasCompany)
            {
                filter.CompanyID = _companyContext.CompanyID;
            }
            else
            {
                filter.CompanyID = null;
            }

            var filterDto = filter.ToDto();
            var pagedResult = await _salesService.GetPagedSalesAsync(filterDto, cancellationToken);

            var customers = await _lookupService.GetCustomersAsync(cancellationToken);
            var warehouses = await _lookupService.GetWarehousesAsync(cancellationToken);
            var Suppliers = await _lookupService.GetSuppliersAsync(cancellationToken);

            var viewModel = new SalesListViewModel
            {
                Items = pagedResult,
                Filter = filter,
                Customers = new SelectList(customers, "Id", "Name", filter.CustomerID),
                Warehouses = new SelectList(warehouses, "Id", "Name", filter.WarehouseID),
                Suppliers = new SelectList(Suppliers, "Id", "Name", filter.SupplierID),
                PaymentStatuses = new SelectList(new[]
                {
                    new { Value = "UNPAID", Text = "Unpaid" },
                    new { Value = "PARTIAL", Text = "Partially Paid" },
                    new { Value = "PAID", Text = "Paid" }
                }, "Value", "Text", filter.PaymentStatus)
            };

            return View(viewModel);
        }

        // GET: Sales/Create
        [HttpGet]
        public async Task<IActionResult> Create(CancellationToken cancellationToken)
        {
            await _companyContext.TryResolveAsync(cancellationToken);
            var blocked = CompanyScopeGuards.RedirectIfCannotCreate(this, _companyContext);
            if (blocked != null) return blocked;

            int userId = GetCurrentUserId();
            var mainWarehouseId = await _lookupService.GetMainWarehouseIdAsync(cancellationToken);
            var viewModel = new CreateSalesViewModel
            {
                InvoiceDate = DateTime.Today,
                SalespersonID = userId,
                WarehouseID = mainWarehouseId ?? 0
            };

            await PopulateDropdownsAsync(viewModel, cancellationToken);
            return View(viewModel);
        }

        // POST: Sales/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateSalesViewModel model, CancellationToken cancellationToken)
        {
            await _companyContext.TryResolveAsync(cancellationToken);
            var blocked = CompanyScopeGuards.RedirectIfCannotCreate(this, _companyContext);
            if (blocked != null) return blocked;

            if (!ModelState.IsValid)
            {
                await PopulateDropdownsAsync(model, cancellationToken);
                return View(model);
            }

            var dto = model.ToDto();
            int userId = GetCurrentUserId();
            // Salesperson is always the logged-in user — never trust a posted value.
            dto.SalespersonID = userId;
            model.SalespersonID = userId;

            var result = await _salesService.CreateAndFinalizeSalesInvoiceAsync(dto, userId, cancellationToken);

            if (!result.Success)
            {
                foreach (var err in result.Errors)
                {
                    ModelState.AddModelError("", err);
                }
                await PopulateDropdownsAsync(model, cancellationToken);
                return View(model);
            }

            TempData["SuccessMessage"] = $"Sales Invoice #{model.InvoiceNumber ?? result.Data.ToString()} created and finalized successfully!";
            return RedirectToAction(nameof(Details), new { id = result.Data });
        }

        // GET: Sales/Details/5
        [HttpGet]
        public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
        {
            await _companyContext.TryResolveAsync(cancellationToken);

            var detailsDto = await _salesService.GetSalesDetailsAsync(id, cancellationToken);
            if (detailsDto == null)
            {
                TempData["ErrorMessage"] = "Sales invoice not found.";
                return RedirectToAction(nameof(Index));
            }

            if (CompanyScopeGuards.IsOutOfScope(_companyContext, detailsDto.Header.CompanyID ?? 0))
            {
                return NotFound();
            }

            var viewModel = detailsDto.ToViewModel();
            return View(viewModel);
        }

        // GET: Sales/PaymentHistoryTab/5?pageNumber=1
        [HttpGet]
        public async Task<IActionResult> PaymentHistoryTab(int id, int pageNumber = 1, CancellationToken cancellationToken = default)
        {
            await _companyContext.TryResolveAsync(cancellationToken);
            var details = await _salesService.GetSalesDetailsAsync(id, cancellationToken);
            if (details == null || CompanyScopeGuards.IsOutOfScope(_companyContext, details.Header.CompanyID ?? 0))
            {
                return NotFound();
            }

            var history = await _salesService.GetPaymentHistoryTabAsync(id, pageNumber, 10, cancellationToken);
            return PartialView("_PaymentHistoryTab", history);
        }

        // GET: Sales/LedgerTab/5?pageNumber=1
        [HttpGet]
        public async Task<IActionResult> LedgerTab(int id, int pageNumber = 1, CancellationToken cancellationToken = default)
        {
            await _companyContext.TryResolveAsync(cancellationToken);
            var details = await _salesService.GetSalesDetailsAsync(id, cancellationToken);
            if (details == null || CompanyScopeGuards.IsOutOfScope(_companyContext, details.Header.CompanyID ?? 0))
            {
                return NotFound();
            }

            var ledger = await _salesService.GetLedgerTabAsync(id, pageNumber, 10, cancellationToken);
            return PartialView("_LedgerTab", ledger);
        }

        // ===== AJAX LOOKUP & EVALUATION ENDPOINTS =====

        [HttpGet]
        public async Task<IActionResult> GetProducts(CancellationToken cancellationToken)
        {
            await _companyContext.TryResolveAsync(cancellationToken);
            if (!_companyContext.HasCompany)
            {
                return BadRequest(new { message = CompanyScopeGuards.SelectSpecificCompanyMessage });
            }

            var products = await _lookupService.GetProductsByCompanyAsync(_companyContext.CompanyID, cancellationToken);
            return Json(products);
        }

        [HttpGet]
        public async Task<IActionResult> GetProductUnitPrice(int productId, int productUnitId, CancellationToken cancellationToken)
        {
            await _companyContext.TryResolveAsync(cancellationToken);
            if (!await ProductMatchesScopeAsync(productId, cancellationToken))
            {
                return NotFound();
            }

            var priceInfo = await _lookupService.GetProductUnitPriceAsync(productId, productUnitId, cancellationToken);
            if (priceInfo == null)
            {
                return Json(new { price = 0m, unitPrice = 0m });
            }
            return Json(new
            {
                price = priceInfo.UnitPrice,
                unitPrice = priceInfo.UnitPrice,
                productUnitId = priceInfo.ProductUnitID,
                unitName = priceInfo.UnitName
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetAvailableStock(int productId, int warehouseId, int productUnitId, int unitId, CancellationToken cancellationToken)
        {
            await _companyContext.TryResolveAsync(cancellationToken);
            if (!await ProductMatchesScopeAsync(productId, cancellationToken))
            {
                return NotFound();
            }

            int targetUnitId = productUnitId > 0 ? productUnitId : unitId;
            var stockInfo = await _lookupService.GetAvailableStockAsync(productId, warehouseId, targetUnitId, cancellationToken);
            return Json(stockInfo);
        }

        [HttpGet]
        public async Task<IActionResult> GetCustomerInfo(int customerId, CancellationToken cancellationToken)
        {
            var info = await _lookupService.GetCustomerInfoAsync(customerId, cancellationToken);
            return Json(info);
        }

        [HttpGet]
        public async Task<IActionResult> GetSubAreas(int? areaId, CancellationToken cancellationToken)
        {
            var subAreas = await _lookupService.GetSubAreasAsync(areaId, cancellationToken);
            return Json(subAreas);
        }

        [HttpGet]
        public async Task<IActionResult> GetCustomers(int? areaId, int? subAreaId, CancellationToken cancellationToken)
        {
            var customers = await _lookupService.GetCustomersAsync(areaId, subAreaId, cancellationToken);
            return Json(customers);
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> GetProductUnits(int productId, CancellationToken cancellationToken)
        {
            if (productId <= 0)
            {
                return Json(new object[0]);
            }

            try
            {
                await _companyContext.TryResolveAsync(cancellationToken);
                if (!await ProductMatchesScopeAsync(productId, cancellationToken))
                {
                    return NotFound();
                }

                var productUnits = await _context.ProductUnits
                    .AsNoTracking()
                    .Include(pu => pu.Product)
                    .Include(pu => pu.Unit)
                    .Where(pu => pu.ProductID == productId && !pu.IsDeleted && pu.IsActive)
                    .OrderByDescending(pu => pu.IsDefaultSalesUnit)
                    .ThenBy(pu => pu.ProductUnitID)
                    .Select(pu => new
                    {
                        id = pu.ProductUnitID,
                        productUnitId = pu.ProductUnitID,
                        name = pu.Unit != null ? pu.Unit.UnitName : "Unit",
                        unitName = pu.Unit != null ? pu.Unit.UnitName : "Unit",
                        salePrice = pu.SellingPrice ?? (pu.Product != null ? pu.Product.BaseSellingPrice * pu.ConversionToBaseUnit : 0m),
                        sellingPrice = pu.SellingPrice ?? (pu.Product != null ? pu.Product.BaseSellingPrice * pu.ConversionToBaseUnit : 0m),
                        conversionFactor = pu.ConversionToBaseUnit,
                        conversionToBaseUnit = pu.ConversionToBaseUnit
                    })
                    .ToListAsync(cancellationToken);

                return Json(productUnits);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetProductUnits failed for ProductID {ProductID}", productId);
                return StatusCode(500, new { success = false, message = "Unable to load product units." });
            }
        }


        [HttpGet]
        public async Task<IActionResult> GetProductInfo(int productId, CancellationToken cancellationToken)
        {
            await _companyContext.TryResolveAsync(cancellationToken);
            if (!await ProductMatchesScopeAsync(productId, cancellationToken))
            {
                return NotFound();
            }

            var info = await _lookupService.GetProductInfoAsync(productId, cancellationToken);
            return Json(info);
        }

        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> EvaluatePromotionsAndDiscounts([FromBody] OrderContextDto context, CancellationToken cancellationToken)
        {
            if (context == null)
            {
                return Json(new EvaluationResultDto());
            }

            // Never trust client CompanyID when a specific company is selected.
            if (await _companyContext.TryResolveAsync(cancellationToken) && _companyContext.HasCompany)
            {
                context.CompanyID = _companyContext.CompanyID;
            }

            var evaluation = await _promotionDiscountService.EvaluatePromotionsAndDiscountsAsync(context, cancellationToken);
            return Json(evaluation);
        }

        [HttpGet]
        public async Task<IActionResult> DownloadPdf(int id, CancellationToken cancellationToken)
        {
            await _companyContext.TryResolveAsync(cancellationToken);

            var details = await _salesService.GetSalesDetailsAsync(id, cancellationToken);
            if (details == null || CompanyScopeGuards.IsOutOfScope(_companyContext, details.Header.CompanyID ?? 0))
            {
                return NotFound();
            }

            var pdfBytes = _pdfService.GenerateSalesInvoicePdf(details);
            return File(pdfBytes, "application/pdf", $"SalesInvoice_{details.Header.InvoiceNumber}.pdf");
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
        {
            await _companyContext.TryResolveAsync(cancellationToken);

            var checkResult = await _salesService.CanEditSalesInvoiceAsync(id, cancellationToken);
            if (!checkResult.Success)
            {
                TempData["ErrorMessage"] = checkResult.Message;
                return RedirectToAction("Details", new { id });
            }

            var details = await _salesService.GetSalesDetailsAsync(id, cancellationToken);
            if (details == null || CompanyScopeGuards.IsOutOfScope(_companyContext, details.Header.CompanyID ?? 0))
            {
                return NotFound();
            }

            var initialItems = details.Items.Select(i => new CreateSalesItemDto
            {
                ProductID = i.ProductID ?? 0,
                ProductUnitID = i.ProductUnitID ?? 0,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                DiscountAmount = i.DiscountAmount,
                ItemType = i.ItemType,
                PromotionID = i.PromotionID,
                IsCustomFreeItem = !string.IsNullOrWhiteSpace(i.CustomItemName),
                CustomFreeItemName = i.CustomItemName
            }).ToList();

            var vm = new EditSalesViewModel
            {
                InvoiceID = details.Header.InvoiceID,
                InvoiceNumber = details.Header.InvoiceNumber,
                CompanyID = details.Header.CompanyID ?? 0,
                CompanyName = details.Header.CompanyName ?? string.Empty,
                CustomerID = details.Header.CustomerID,
                CustomerName = details.Header.CustomerName,
                BookerID = details.Header.BookerID ?? 0,
                SalespersonID = details.Header.SalespersonID ?? 0,
                WarehouseID = details.Header.WarehouseID,
                WarehouseName = details.Header.WarehouseName,
                SupplierID = details.Header.SupplierID,
                InvoiceDate = details.Header.InvoiceDate,
                AppliedDiscountRuleID = details.Header.AppliedDiscountRuleID,
                DiscountMode = string.IsNullOrWhiteSpace(details.Header.DiscountMode) ? "None" : details.Header.DiscountMode,
                ApplyPromotions = initialItems.Any(i => string.Equals(i.ItemType, "FREE", StringComparison.OrdinalIgnoreCase)),
                ItemsJson = JsonSerializer.Serialize(initialItems)
            };

            await PopulateEditDropdownsAsync(vm, cancellationToken);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditSalesViewModel model, CancellationToken cancellationToken)
        {
            await _companyContext.TryResolveAsync(cancellationToken);

            var existing = await _salesService.GetSalesDetailsAsync(model.InvoiceID, cancellationToken);
            if (existing == null || CompanyScopeGuards.IsOutOfScope(_companyContext, existing.Header.CompanyID ?? 0))
            {
                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(model.EditReason))
            {
                ModelState.AddModelError("EditReason", "An edit reason is required.");
            }

            List<CreateSalesItemDto> items = new List<CreateSalesItemDto>();
            try
            {
                items = JsonSerializer.Deserialize<List<CreateSalesItemDto>>(model.ItemsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<CreateSalesItemDto>();
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
                await PopulateEditDropdownsAsync(model, cancellationToken);
                return View(model);
            }

            var updateDto = new UpdateSalesInvoiceDto
            {
                InvoiceID = model.InvoiceID,
                BookerID = model.BookerID,
                SalespersonID = model.SalespersonID,
                InvoiceDate = model.InvoiceDate,
                SupplierID = model.SupplierID,
                Remarks = model.Remarks,
                AppliedDiscountRuleID = model.AppliedDiscountRuleID,
                DiscountMode = model.DiscountMode,
                ManualDiscountType = model.ManualDiscountType,
                ManualDiscountValue = model.ManualDiscountValue,
                ApplyPromotions = model.ApplyPromotions,
                EditReason = model.EditReason,
                Items = items
            };

            int userId = GetCurrentUserId();
            var result = await _salesService.UpdateSalesInvoiceAsync(updateDto, userId, cancellationToken);

            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.Message);
                await PopulateEditDropdownsAsync(model, cancellationToken);
                return View(model);
            }

            TempData["SuccessMessage"] = result.Message;
            return RedirectToAction("Details", new { id = model.InvoiceID });
        }

        private async Task PopulateDropdownsAsync(CreateSalesViewModel model, CancellationToken cancellationToken)
        {
            await _companyContext.TryResolveAsync(cancellationToken);
            int companyId = _companyContext.CompanyID;
            model.CompanyName = _companyContext.CompanyName;

            var areas = await _lookupService.GetAreasAsync(cancellationToken);
            model.Areas = areas.Select(a => new SelectListItem
            {
                Value = a.Id.ToString(),
                Text = a.Name,
                Selected = a.Id == model.AreaID
            }).ToList();

            if (model.AreaID.HasValue && model.AreaID.Value > 0)
            {
                var subAreas = await _lookupService.GetSubAreasAsync(model.AreaID.Value, cancellationToken);
                model.SubAreas = subAreas.Select(sa => new SelectListItem
                {
                    Value = sa.Id.ToString(),
                    Text = sa.Name,
                    Selected = sa.Id == model.SubAreaID
                }).ToList();
            }
            else
            {
                model.SubAreas = new List<SelectListItem>();
            }

            if (model.SubAreaID.HasValue && model.SubAreaID.Value > 0)
            {
                var customers = await _lookupService.GetCustomersAsync(model.AreaID, model.SubAreaID, cancellationToken);
                model.Customers = new SelectList(customers, "Id", "Name", model.CustomerID);
            }
            else
            {
                model.Customers = new SelectList(Enumerable.Empty<SelectListItem>());
            }

            var bookers = await _lookupService.GetBookersAsync(companyId, cancellationToken);
            var salespersons = await _lookupService.GetSalespersonsAsync(cancellationToken);
            var warehouses = await _lookupService.GetWarehousesAsync(cancellationToken);
            var Suppliers = await _lookupService.GetSuppliersAsync(cancellationToken);
            var productsLookup = await _lookupService.GetProductsByCompanyAsync(companyId, cancellationToken);

            model.Bookers = new SelectList(bookers, "Id", "Name", model.BookerID);
            model.Salespersons = new SelectList(salespersons, "Id", "Name", model.SalespersonID);
            model.Warehouses = new SelectList(warehouses, "Id", "Name", model.WarehouseID);
            model.Suppliers = new SelectList(Suppliers, "Id", "Name", model.SupplierID);
            model.Products = productsLookup.Select(p => new SelectListItem
            {
                Value = p.Id.ToString(),
                Text = p.Name
            }).ToList();
        }

        private async Task PopulateEditDropdownsAsync(EditSalesViewModel model, CancellationToken cancellationToken)
        {
            int companyId = model.CompanyID > 0
                ? model.CompanyID
                : (await _context.SalesInvoices.AsNoTracking()
                    .Where(si => si.InvoiceID == model.InvoiceID)
                    .Select(si => si.CompanyID)
                    .FirstOrDefaultAsync(cancellationToken));

            if (string.IsNullOrWhiteSpace(model.CompanyName) && companyId > 0)
            {
                model.CompanyName = await _context.Companies.AsNoTracking()
                    .Where(c => c.CompanyID == companyId)
                    .Select(c => c.CompanyName)
                    .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;
            }

            model.CompanyID = companyId;

            var bookers = await _lookupService.GetBookersAsync(companyId, cancellationToken);
            var salespersons = await _lookupService.GetSalespersonsAsync(cancellationToken);
            var Suppliers = await _lookupService.GetSuppliersAsync(cancellationToken);

            model.Bookers = new SelectList(bookers, "Id", "Name", model.BookerID);
            model.Salespersons = new SelectList(salespersons, "Id", "Name", model.SalespersonID);
            model.Suppliers = new SelectList(Suppliers, "Id", "Name", model.SupplierID);
        }

        /// <summary>
        /// When a specific company is selected, the product must belong to that company.
        /// In All Companies mode the check is skipped (returns true if the product exists).
        /// </summary>
        private async Task<bool> ProductMatchesScopeAsync(int productId, CancellationToken cancellationToken)
        {
            if (productId <= 0)
            {
                return false;
            }

            var productCompanyId = await _context.Products
                .AsNoTracking()
                .Where(p => p.ProductID == productId && !p.IsDeleted)
                .Select(p => (int?)p.CompanyID)
                .FirstOrDefaultAsync(cancellationToken);

            if (!productCompanyId.HasValue)
            {
                return false;
            }

            return !CompanyScopeGuards.IsOutOfScope(_companyContext, productCompanyId.Value);
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
