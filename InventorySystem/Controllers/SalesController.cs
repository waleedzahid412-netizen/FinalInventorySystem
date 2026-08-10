using System;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using InventorySystem.DTOs.Sales;
using InventorySystem.Mappings;
using InventorySystem.Services.Interfaces;
using InventorySystem.ViewModels.Sales;

namespace InventorySystem.Controllers
{
    [Authorize]
    public class SalesController : Controller
    {
        private readonly ISalesService _salesService;
        private readonly ILookupService _lookupService;
        private readonly IPromotionDiscountService _promotionDiscountService;
        private readonly IPdfService _pdfService;
        private readonly InventorySystem.Data.ApplicationDbContext _context;
        private readonly Microsoft.Extensions.Logging.ILogger<SalesController> _logger;

        public SalesController(
            ISalesService salesService,
            ILookupService lookupService,
            IPromotionDiscountService promotionDiscountService,
            IPdfService pdfService,
            InventorySystem.Data.ApplicationDbContext context,
            Microsoft.Extensions.Logging.ILogger<SalesController> logger)
        {
            _salesService = salesService;
            _lookupService = lookupService;
            _promotionDiscountService = promotionDiscountService;
            _pdfService = pdfService;
            _context = context;
            _logger = logger;
        }

        // GET: Sales
        [HttpGet]
        public async Task<IActionResult> Index(SalesFilterViewModel filter, CancellationToken cancellationToken)
        {
            var filterDto = filter.ToDto();
            var pagedResult = await _salesService.GetPagedSalesAsync(filterDto, cancellationToken);

            var customers = await _lookupService.GetCustomersAsync(cancellationToken);
            var warehouses = await _lookupService.GetWarehousesAsync(cancellationToken);
            var deliveryPersons = await _lookupService.GetDeliveryPersonsAsync(cancellationToken);

            var viewModel = new SalesListViewModel
            {
                Items = pagedResult,
                Filter = filter,
                Customers = new SelectList(customers, "Id", "Name", filter.CustomerID),
                Warehouses = new SelectList(warehouses, "Id", "Name", filter.WarehouseID),
                DeliveryPersons = new SelectList(deliveryPersons, "Id", "Name", filter.DeliveryPersonID),
                PaymentStatuses = new SelectList(new[]
                {
                    new { Value = "UNPAID", Text = "Unpaid" },
                    new { Value = "PARTIAL", Text = "Partially Paid" },
                    new { Value = "PAID", Text = "Paid" }
                }, "Value", "Text", filter.PaymentStatus)
            };

            return View(viewModel);
        }

        // GET: Sales/CreateQuick
        [HttpGet]
        public async Task<IActionResult> CreateQuick(CancellationToken cancellationToken)
        {
            var viewModel = new CreateSalesViewModel
            {
                InvoiceDate = DateTime.Today
            };

            await PopulateDropdownsAsync(viewModel, cancellationToken);
            return View(viewModel);
        }

        // GET: Sales/Create
        [HttpGet]
        public async Task<IActionResult> Create(CancellationToken cancellationToken)
        {
            var viewModel = new CreateSalesViewModel
            {
                InvoiceDate = DateTime.Today
            };

            await PopulateDropdownsAsync(viewModel, cancellationToken);
            return View(viewModel);
        }

        // POST: Sales/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateSalesViewModel model, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                await PopulateDropdownsAsync(model, cancellationToken);
                return View(model);
            }

            var dto = model.ToDto();
            int userId = GetCurrentUserId();

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
            var detailsDto = await _salesService.GetSalesDetailsAsync(id, cancellationToken);
            if (detailsDto == null)
            {
                TempData["ErrorMessage"] = "Sales invoice not found.";
                return RedirectToAction(nameof(Index));
            }

            var viewModel = detailsDto.ToViewModel();
            return View(viewModel);
        }

        // GET: Sales/PaymentHistoryTab/5?pageNumber=1
        [HttpGet]
        public async Task<IActionResult> PaymentHistoryTab(int id, int pageNumber = 1, CancellationToken cancellationToken = default)
        {
            var history = await _salesService.GetPaymentHistoryTabAsync(id, pageNumber, 10, cancellationToken);
            return PartialView("_PaymentHistoryTab", history);
        }

        // GET: Sales/LedgerTab/5?pageNumber=1
        [HttpGet]
        public async Task<IActionResult> LedgerTab(int id, int pageNumber = 1, CancellationToken cancellationToken = default)
        {
            var ledger = await _salesService.GetLedgerTabAsync(id, pageNumber, 10, cancellationToken);
            return PartialView("_LedgerTab", ledger);
        }

        // ===== AJAX LOOKUP & EVALUATION ENDPOINTS =====

        [HttpGet]
        public async Task<IActionResult> GetProducts(CancellationToken cancellationToken)
        {
            var products = await _lookupService.GetProductsAsync(cancellationToken);
            return Json(products);
        }

        [HttpGet]
        public async Task<IActionResult> GetProductUnitPrice(int productId, int productUnitId, CancellationToken cancellationToken)
        {
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
        public async Task<IActionResult> GetProductsByCompany(int companyId, CancellationToken cancellationToken)
        {
            var products = await _lookupService.GetProductsByCompanyAsync(companyId, cancellationToken);
            return Json(products);
        }

        [HttpGet]
        public async Task<IActionResult> GetProductsByWarehouse(int warehouseId, CancellationToken cancellationToken)
        {
            var products = await _context.Products
                .AsNoTracking()
                .Where(p => !p.IsDeleted && p.IsActive)
                .OrderBy(p => p.ProductName)
                .Select(p => new
                {
                    id = p.ProductID,
                    productID = p.ProductID,
                    name = string.IsNullOrWhiteSpace(p.SKU) ? p.ProductName : $"{p.ProductName} ({p.SKU})",
                    productName = p.ProductName
                })
                .ToListAsync(cancellationToken);

            return Json(products);
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
            var info = await _lookupService.GetProductInfoAsync(productId, cancellationToken);
            return Json(info);
        }

        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> EvaluatePromotionsAndDiscounts([FromBody] OrderContextDto context, CancellationToken cancellationToken)
        {
            var evaluation = await _promotionDiscountService.EvaluatePromotionsAndDiscountsAsync(context, cancellationToken);
            return Json(evaluation);
        }

        [HttpGet]
        public async Task<IActionResult> DownloadPdf(int id, CancellationToken cancellationToken)
        {
            var details = await _salesService.GetSalesDetailsAsync(id, cancellationToken);
            if (details == null)
            {
                return NotFound();
            }

            var pdfBytes = _pdfService.GenerateSalesInvoicePdf(details);
            return File(pdfBytes, "application/pdf", $"SalesInvoice_{details.Header.InvoiceNumber}.pdf");
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
        {
            var checkResult = await _salesService.CanEditSalesInvoiceAsync(id, cancellationToken);
            if (!checkResult.Success)
            {
                TempData["ErrorMessage"] = checkResult.Message;
                return RedirectToAction("Details", new { id });
            }

            var details = await _salesService.GetSalesDetailsAsync(id, cancellationToken);
            if (details == null)
            {
                return NotFound();
            }

            var deliveryPersons = await _lookupService.GetDeliveryPersonsAsync(cancellationToken);
            var initialItems = details.Items.Select(i => new CreateSalesItemDto
            {
                ProductID = i.ProductID,
                ProductUnitID = i.ProductUnitID,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                DiscountAmount = i.DiscountAmount,
                ItemType = i.ItemType,
                PromotionID = i.PromotionID
            }).ToList();

            var vm = new EditSalesViewModel
            {
                InvoiceID = details.Header.InvoiceID,
                InvoiceNumber = details.Header.InvoiceNumber,
                CustomerID = details.Header.CustomerID,
                CustomerName = details.Header.CustomerName,
                WarehouseID = details.Header.WarehouseID,
                WarehouseName = details.Header.WarehouseName,
                DeliveryPersonID = details.Header.DeliveryPersonID,
                InvoiceDate = details.Header.InvoiceDate,
                AppliedDiscountRuleID = details.Header.AppliedDiscountRuleID,
                ItemsJson = JsonSerializer.Serialize(initialItems),
                DeliveryPersons = new SelectList(deliveryPersons, "Id", "Name", details.Header.DeliveryPersonID)
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditSalesViewModel model, CancellationToken cancellationToken)
        {
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
                var deliveryPersons = await _lookupService.GetDeliveryPersonsAsync(cancellationToken);
                model.DeliveryPersons = new SelectList(deliveryPersons, "Id", "Name", model.DeliveryPersonID);
                return View(model);
            }

            var updateDto = new UpdateSalesInvoiceDto
            {
                InvoiceID = model.InvoiceID,
                InvoiceDate = model.InvoiceDate,
                DeliveryPersonID = model.DeliveryPersonID,
                Remarks = model.Remarks,
                AppliedDiscountRuleID = model.AppliedDiscountRuleID,
                EditReason = model.EditReason,
                Items = items
            };

            int userId = GetCurrentUserId();
            var result = await _salesService.UpdateSalesInvoiceAsync(updateDto, userId, cancellationToken);

            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.Message);
                var deliveryPersons = await _lookupService.GetDeliveryPersonsAsync(cancellationToken);
                model.DeliveryPersons = new SelectList(deliveryPersons, "Id", "Name", model.DeliveryPersonID);
                return View(model);
            }

            TempData["SuccessMessage"] = result.Message;
            return RedirectToAction("Details", new { id = model.InvoiceID });
        }

        private async Task PopulateDropdownsAsync(CreateSalesViewModel model, CancellationToken cancellationToken)
        {
            var customers = await _lookupService.GetCustomersAsync(cancellationToken);
            var warehouses = await _lookupService.GetWarehousesAsync(cancellationToken);
            var deliveryPersons = await _lookupService.GetDeliveryPersonsAsync(cancellationToken);
            var products = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(_context.Products
                .AsNoTracking()
                .Where(p => !p.IsDeleted && p.IsActive)
                .OrderBy(p => p.ProductName)
                .Select(p => new SelectListItem
                {
                    Value = p.ProductID.ToString(),
                    Text = string.IsNullOrWhiteSpace(p.SKU) ? p.ProductName : $"{p.ProductName} ({p.SKU})"
                }), cancellationToken);

            model.Customers = new SelectList(customers, "Id", "Name", model.CustomerID);
            model.Warehouses = new SelectList(warehouses, "Id", "Name", model.WarehouseID);
            model.DeliveryPersons = new SelectList(deliveryPersons, "Id", "Name", model.DeliveryPersonID);
            model.Products = products;
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
