using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using InventorySystem.DTOs.Returns;
using InventorySystem.Services.Interfaces;
using InventorySystem.ViewModels.Returns;

namespace InventorySystem.Controllers
{
    [Authorize]
    public class ReturnsController : Controller
    {
        private readonly IReturnService _returnService;
        private readonly ILookupService _lookupService;
        private readonly IPdfService _pdfService;
        private readonly InventorySystem.Data.ApplicationDbContext _context;

        public ReturnsController(
            IReturnService returnService,
            ILookupService lookupService,
            IPdfService pdfService,
            InventorySystem.Data.ApplicationDbContext context)
        {
            _returnService = returnService ?? throw new ArgumentNullException(nameof(returnService));
            _lookupService = lookupService ?? throw new ArgumentNullException(nameof(lookupService));
            _pdfService = pdfService ?? throw new ArgumentNullException(nameof(pdfService));
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        // GET: /Returns/CustomerReturns
        [HttpGet]
        public async Task<IActionResult> CustomerReturns([FromQuery] SalesReturnFilterDto filter, CancellationToken cancellationToken = default)
        {
            filter ??= new SalesReturnFilterDto();
            var returnsResult = await _returnService.GetPagedSalesReturnsAsync(filter, cancellationToken);
            var customers = await _lookupService.GetCustomersAsync(cancellationToken);

            var viewModel = new CustomerReturnsListViewModel
            {
                Filter = filter,
                Returns = returnsResult,
                Customers = customers.Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = c.Name,
                    Selected = filter.CustomerID == c.Id
                })
            };

            return View(viewModel);
        }

        // GET: /Returns/SalesReturnDetails/5
        [HttpGet]
        public async Task<IActionResult> SalesReturnDetails(int id, CancellationToken cancellationToken = default)
        {
            if (id <= 0) return NotFound();

            var details = await _returnService.GetSalesReturnDetailsAsync(id, cancellationToken);
            if (details == null) return NotFound();

            return View(details);
        }

        // GET: /Returns/ProcessSalesReturn?invoiceId=5
        [HttpGet]
        public async Task<IActionResult> ProcessSalesReturn(int invoiceId, CancellationToken cancellationToken = default)
        {
            if (invoiceId <= 0)
            {
                TempData["ErrorMessage"] = "Please select a valid sales invoice to process a return.";
                return RedirectToAction(nameof(CustomerReturns));
            }

            var eligibilityResult = await _returnService.GetSalesReturnEligibilityAsync(invoiceId, cancellationToken);
            if (!eligibilityResult.Success || eligibilityResult.Data == null)
            {
                TempData["ErrorMessage"] = eligibilityResult.Message;
                return RedirectToAction(nameof(CustomerReturns));
            }

            var viewModel = new ProcessSalesReturnViewModel
            {
                Eligibility = eligibilityResult.Data,
                Request = new ProcessSalesReturnRequest
                {
                    SalesInvoiceID = invoiceId,
                    ReturnDate = DateTime.UtcNow,
                    IncludeSchemeCalculation = true
                }
            };

            return View(viewModel);
        }

        // POST: /Returns/PreviewSalesClawback (AJAX)
        [HttpPost]
        public async Task<IActionResult> PreviewSalesClawback([FromBody] PreviewReturnRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                if (request == null) return BadRequest(new { success = false, message = "Invalid request." });

                var result = await _returnService.PreviewSalesClawbackAsync(request, cancellationToken);
                if (!result.Success)
                {
                    return Json(new { success = false, message = result.Message });
                }

                return Json(new { success = true, data = result.Data });
            }
            catch (Exception)
            {
                return StatusCode(500, new { success = false, message = "An unexpected error occurred while calculating the refund preview." });
            }
        }

        // POST: /Returns/ProcessSalesReturn (AJAX)
        [HttpPost]
        public async Task<IActionResult> ProcessSalesReturn([FromBody] ProcessSalesReturnRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                if (request == null) return BadRequest(new { success = false, message = "Invalid return request." });

                int userId = GetCurrentUserId();
                var result = await _returnService.ProcessSalesReturnAsync(request, userId, cancellationToken);

                if (!result.Success)
                {
                    return Json(new { success = false, message = result.Message });
                }

                return Json(new { success = true, returnId = result.Data, message = result.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { success = false, message = "An unexpected error occurred while processing the sales return." });
            }
        }

        // GET: /Returns/CreateReturn
        [HttpGet]
        public async Task<IActionResult> CreateReturn(CancellationToken cancellationToken = default)
        {
            var customers = await _lookupService.GetCustomersAsync(cancellationToken);
            var warehouses = await _lookupService.GetWarehousesAsync(cancellationToken);
            var products = await _context.Products
                .AsNoTracking()
                .Where(p => !p.IsDeleted && p.IsActive)
                .OrderBy(p => p.ProductName)
                .Select(p => new SelectListItem
                {
                    Value = p.ProductID.ToString(),
                    Text = string.IsNullOrWhiteSpace(p.SKU) ? p.ProductName : $"{p.ProductName} ({p.SKU})"
                })
                .ToListAsync(cancellationToken);

            var viewModel = new InventorySystem.ViewModels.Returns.CreateReturnViewModel
            {
                Customers = customers.Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name }),
                Warehouses = warehouses.Select(w => new SelectListItem { Value = w.Id.ToString(), Text = w.Name }),
                Products = products
            };

            return View(viewModel);
        }

        // GET: /Returns/GetProductUnitPrice?productId=X&productUnitId=Y
        [HttpGet]
        public async Task<IActionResult> GetProductUnitPrice(int productId, int productUnitId, CancellationToken cancellationToken = default)
        {
            if (productId <= 0 || productUnitId <= 0) return Json(new { price = 0m });

            var pu = await _context.ProductUnits
                .Include(u => u.Product)
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.ProductID == productId && u.ProductUnitID == productUnitId, cancellationToken);

            if (pu == null) return Json(new { price = 0m });

            decimal price = pu.SellingPrice ?? (pu.Product.BaseSellingPrice * pu.ConversionToBaseUnit);
            return Json(new { price = price });
        }

        // GET: /Returns/GetCustomerInvoices?customerId=X
        [HttpGet]
        public async Task<IActionResult> GetCustomerInvoices(int customerId, CancellationToken cancellationToken = default)
        {
            if (customerId <= 0) return Json(new object[0]);

            var invoices = await _context.SalesInvoices
                .AsNoTracking()
                .Where(i => !i.IsDeleted && i.CustomerID == customerId)
                .OrderByDescending(i => i.InvoiceDate)
                .Select(i => new
                {
                    invoiceId = i.InvoiceID,
                    invoiceNumber = i.InvoiceNumber,
                    invoiceDate = i.InvoiceDate.ToString("dd MMM yyyy"),
                    grandTotal = i.GrandTotal,
                    paidAmount = i.PaidAmount,
                    balance = i.GrandTotal - i.PaidAmount
                })
                .ToListAsync(cancellationToken);

            return Json(invoices);
        }

        // GET: /Returns/GetSalesReturnEligibilityJson?invoiceId=X
        [HttpGet]
        public async Task<IActionResult> GetSalesReturnEligibilityJson(int invoiceId, CancellationToken cancellationToken = default)
        {
            if (invoiceId <= 0) return Json(new { success = false, message = "Invalid invoice ID." });

            var result = await _returnService.GetSalesReturnEligibilityAsync(invoiceId, cancellationToken);
            if (!result.Success || result.Data == null)
            {
                return Json(new { success = false, message = result.Message });
            }

            return Json(new { success = true, data = result.Data });
        }

        // GET: /Returns/ProcessManualSalesReturn
        [HttpGet]
        public async Task<IActionResult> ProcessManualSalesReturn(CancellationToken cancellationToken = default)
        {
            var customers = await _lookupService.GetCustomersAsync(cancellationToken);
            var warehouses = await _lookupService.GetWarehousesAsync(cancellationToken);
            var products = await _context.Products
                .AsNoTracking()
                .Where(p => !p.IsDeleted && p.IsActive)
                .OrderBy(p => p.ProductName)
                .Select(p => new SelectListItem
                {
                    Value = p.ProductID.ToString(),
                    Text = string.IsNullOrWhiteSpace(p.SKU) ? p.ProductName : $"{p.ProductName} ({p.SKU})"
                })
                .ToListAsync(cancellationToken);

            var viewModel = new ProcessManualSalesReturnViewModel
            {
                Customers = customers.Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name }),
                Warehouses = warehouses.Select(w => new SelectListItem { Value = w.Id.ToString(), Text = w.Name }),
                Products = products,
                Request = new ProcessManualSalesReturnRequest
                {
                    ReturnDate = DateTime.UtcNow,
                    SettlementMethod = "ACCOUNT_ADJUSTMENT"
                }
            };

            return View(viewModel);
        }

        // POST: /Returns/ProcessManualSalesReturn (AJAX)
        [HttpPost]
        public async Task<IActionResult> ProcessManualSalesReturn([FromBody] ProcessManualSalesReturnRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                if (request == null) return BadRequest(new { success = false, message = "Invalid manual return request." });

                int userId = GetCurrentUserId();
                var result = await _returnService.ProcessManualSalesReturnAsync(request, userId, cancellationToken);

                if (!result.Success)
                {
                    return Json(new { success = false, message = result.Message });
                }

                return Json(new { success = true, returnId = result.Data, message = result.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { success = false, message = "An unexpected error occurred while processing the manual sales return." });
            }
        }

        // GET: /Returns/GetProductUnits?productId=5 (AJAX helper for manual returns)
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> GetProductUnits(int productId, CancellationToken cancellationToken = default)
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
                System.Diagnostics.Trace.TraceError($"Error in ReturnsController.GetProductUnits for ProductID {productId}: {ex}");
                return StatusCode(500, new { success = false, message = "Unable to load product units." });
            }
        }

        // GET: /Returns/CompanyReturns
        [HttpGet]
        public async Task<IActionResult> CompanyReturns([FromQuery] PurchaseReturnFilterDto filter, CancellationToken cancellationToken = default)
        {
            filter ??= new PurchaseReturnFilterDto();
            var returnsResult = await _returnService.GetPagedPurchaseReturnsAsync(filter, cancellationToken);
            var companies = await _lookupService.GetCompaniesAsync(cancellationToken);

            var viewModel = new CompanyReturnsListViewModel
            {
                Filter = filter,
                Returns = returnsResult,
                Companies = companies.Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = c.Name,
                    Selected = filter.CompanyID == c.Id
                })
            };

            return View(viewModel);
        }

        // GET: /Returns/PurchaseReturnDetails/5
        [HttpGet]
        public async Task<IActionResult> PurchaseReturnDetails(int id, CancellationToken cancellationToken = default)
        {
            if (id <= 0) return NotFound();

            var details = await _returnService.GetPurchaseReturnDetailsAsync(id, cancellationToken);
            if (details == null) return NotFound();

            return View(details);
        }

        // GET: /Returns/ProcessPurchaseReturn?invoiceId=5
        [HttpGet]
        public async Task<IActionResult> ProcessPurchaseReturn(int invoiceId, CancellationToken cancellationToken = default)
        {
            if (invoiceId <= 0)
            {
                TempData["ErrorMessage"] = "Please select a valid purchase invoice to process a return.";
                return RedirectToAction(nameof(CompanyReturns));
            }

            var eligibilityResult = await _returnService.GetPurchaseReturnEligibilityAsync(invoiceId, cancellationToken);
            if (!eligibilityResult.Success || eligibilityResult.Data == null)
            {
                TempData["ErrorMessage"] = eligibilityResult.Message;
                return RedirectToAction(nameof(CompanyReturns));
            }

            var viewModel = new ProcessPurchaseReturnViewModel
            {
                Eligibility = eligibilityResult.Data,
                Request = new ProcessPurchaseReturnRequest
                {
                    PurchaseInvoiceID = invoiceId,
                    ReturnDate = DateTime.UtcNow
                }
            };

            return View(viewModel);
        }

        // POST: /Returns/ProcessPurchaseReturn (AJAX)
        [HttpPost]
        public async Task<IActionResult> ProcessPurchaseReturn([FromBody] ProcessPurchaseReturnRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null) return BadRequest(new { success = false, message = "Invalid return request." });

            int userId = GetCurrentUserId();
            var result = await _returnService.ProcessPurchaseReturnAsync(request, userId, cancellationToken);

            if (!result.Success)
            {
                return Json(new { success = false, message = result.Message });
            }

            return Json(new { success = true, returnId = result.Data, message = result.Message });
        }

        // GET: /Returns/DownloadSalesReturnPdf/5
        [HttpGet]
        public async Task<IActionResult> DownloadSalesReturnPdf(int id, CancellationToken cancellationToken = default)
        {
            if (id <= 0) return NotFound();

            var returnDetails = await _returnService.GetSalesReturnDetailsAsync(id, cancellationToken);
            if (returnDetails == null) return NotFound();

            byte[] pdfBytes = _pdfService.GenerateSalesReturnPdf(returnDetails);
            string fileName = $"SalesReturn_{returnDetails.Header.ReturnNumber}.pdf";

            return File(pdfBytes, "application/pdf", fileName);
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (claim != null && int.TryParse(claim.Value, out int userId))
            {
                return userId;
            }
            return 1; // System fallback
        }
    }
}
