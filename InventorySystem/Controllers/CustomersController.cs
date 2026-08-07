using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Logging;
using InventorySystem.DTOs.Customers;
using InventorySystem.Mappings;
using InventorySystem.Services.Interfaces;
using InventorySystem.ViewModels.Customers;

namespace InventorySystem.Controllers
{
    [Authorize]
    public class CustomersController : Controller
    {
        private readonly ICustomerService _customerService;
        private readonly ILookupService _lookupService;
        private readonly ILogger<CustomersController> _logger;

        public CustomersController(
            ICustomerService customerService,
            ILookupService lookupService,
            ILogger<CustomersController> logger)
        {
            _customerService = customerService;
            _lookupService = lookupService;
            _logger = logger;
        }

        // GET: Customers
        [HttpGet]
        public async Task<IActionResult> Index([FromQuery] CustomerFilterDto filter, CancellationToken cancellationToken)
        {
            var pagedCustomers = await _customerService.GetPagedCustomersAsync(filter, cancellationToken);
            var areas = await _lookupService.GetAreasAsync(cancellationToken);
            var subAreas = filter.AreaID.HasValue
                ? await _lookupService.GetSubAreasAsync(filter.AreaID.Value, cancellationToken)
                : await _lookupService.GetSubAreasAsync(null, cancellationToken);

            var viewModel = new CustomerListViewModel
            {
                Filter = filter,
                Customers = pagedCustomers,
                Areas = areas.Select(a => new SelectListItem { Value = a.Id.ToString(), Text = a.Name, Selected = a.Id == filter.AreaID }).ToList(),
                SubAreas = subAreas.Select(sa => new SelectListItem { Value = sa.Id.ToString(), Text = sa.Name, Selected = sa.Id == filter.SubAreaID }).ToList()
            };

            return View(viewModel);
        }

        // GET: Customers/Create
        [HttpGet]
        public async Task<IActionResult> Create(CancellationToken cancellationToken)
        {
            var viewModel = new CreateCustomerViewModel();
            await PopulateDropdownsAsync(viewModel, cancellationToken);
            return View(viewModel);
        }

        // POST: Customers/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateCustomerViewModel model, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                await PopulateDropdownsAsync(model, cancellationToken);
                return View(model);
            }

            int userId = GetCurrentUserId();
            var result = await _customerService.CreateCustomerAsync(model.ToDto(), userId, cancellationToken);

            if (!result.Success)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error);
                }
                await PopulateDropdownsAsync(model, cancellationToken);
                return View(model);
            }

            TempData["SuccessMessage"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        // GET: Customers/Edit/5
        [HttpGet]
        public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
        {
            var editDto = await _customerService.GetCustomerForEditAsync(id, cancellationToken);
            if (editDto == null)
            {
                TempData["ErrorMessage"] = "Customer not found.";
                return RedirectToAction(nameof(Index));
            }

            var viewModel = editDto.ToViewModel();
            await PopulateDropdownsAsync(viewModel, cancellationToken);
            return View(viewModel);
        }

        // POST: Customers/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, EditCustomerViewModel model, CancellationToken cancellationToken)
        {
            if (id != model.CustomerID)
            {
                return BadRequest();
            }

            if (!ModelState.IsValid)
            {
                await PopulateDropdownsAsync(model, cancellationToken);
                return View(model);
            }

            int userId = GetCurrentUserId();
            var result = await _customerService.UpdateCustomerAsync(model.ToDto(), userId, cancellationToken);

            if (!result.Success)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error);
                }
                await PopulateDropdownsAsync(model, cancellationToken);
                return View(model);
            }

            TempData["SuccessMessage"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        // GET: Customers/Details/5
        // Loads ONLY customer info & financial summary initially for high performance.
        [HttpGet]
        public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
        {
            var detailsDto = await _customerService.GetCustomerDetailsAsync(id, cancellationToken);
            if (detailsDto == null)
            {
                TempData["ErrorMessage"] = "Customer not found.";
                return RedirectToAction(nameof(Index));
            }

            return View(detailsDto.ToViewModel());
        }

        // ===== ASYNC TAB PARTIAL ENDPOINTS (Loaded on-demand via AJAX) =====

        [HttpGet]
        public async Task<IActionResult> SalesHistoryTab(int id, int pageNumber = 1, int pageSize = 10, CancellationToken cancellationToken = default)
        {
            var salesHistory = await _customerService.GetSalesHistoryAsync(id, pageNumber, pageSize, cancellationToken);
            ViewBag.CustomerID = id;
            return PartialView("_SalesHistoryTab", salesHistory);
        }

        [HttpGet]
        public async Task<IActionResult> PaymentHistoryTab(int id, int pageNumber = 1, int pageSize = 10, CancellationToken cancellationToken = default)
        {
            var paymentHistory = await _customerService.GetPaymentHistoryAsync(id, pageNumber, pageSize, cancellationToken);
            ViewBag.CustomerID = id;
            return PartialView("_PaymentHistoryTab", paymentHistory);
        }

        [HttpGet]
        public async Task<IActionResult> LedgerTab(int id, int pageNumber = 1, int pageSize = 10, CancellationToken cancellationToken = default)
        {
            var ledger = await _customerService.GetLedgerAsync(id, pageNumber, pageSize, cancellationToken);
            ViewBag.CustomerID = id;
            return PartialView("_LedgerTab", ledger);
        }

        // POST: Customers/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
        {
            int userId = GetCurrentUserId();
            var result = await _customerService.SoftDeleteCustomerAsync(id, userId, cancellationToken);

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
                TempData["ErrorMessage"] = result.Errors.FirstOrDefault() ?? "Failed to delete customer.";
            }

            return RedirectToAction(nameof(Index));
        }

        // ===== AJAX VALIDATION & CASCADING DROPDOWN ENDPOINTS =====

        [HttpGet]
        public async Task<IActionResult> CheckShopName(string shopName, int? excludeCustomerId, CancellationToken cancellationToken)
        {
            bool isUnique = await _customerService.ValidateShopNameAsync(shopName, excludeCustomerId, cancellationToken);
            return Json(isUnique);
        }

        [HttpGet]
        public async Task<IActionResult> CheckPhone(string phone, int? excludeCustomerId, CancellationToken cancellationToken)
        {
            bool isUnique = await _customerService.ValidatePhoneAsync(phone, excludeCustomerId, cancellationToken);
            return Json(isUnique);
        }

        [HttpGet]
        public async Task<IActionResult> GetSubAreas(int? areaId, CancellationToken cancellationToken)
        {
            var subAreas = await _lookupService.GetSubAreasAsync(areaId, cancellationToken);
            return Json(subAreas);
        }

        private async Task PopulateDropdownsAsync(CreateCustomerViewModel model, CancellationToken cancellationToken)
        {
            var areas = await _lookupService.GetAreasAsync(cancellationToken);
            model.Areas = areas.Select(a => new SelectListItem { Value = a.Id.ToString(), Text = a.Name, Selected = a.Id == model.AreaID }).ToList();

            var subAreas = model.AreaID.HasValue
                ? await _lookupService.GetSubAreasAsync(model.AreaID.Value, cancellationToken)
                : new System.Collections.Generic.List<DTOs.Common.LookupItemDto>();

            model.SubAreas = subAreas.Select(sa => new SelectListItem { Value = sa.Id.ToString(), Text = sa.Name, Selected = sa.Id == model.SubAreaID }).ToList();
        }

        private async Task PopulateDropdownsAsync(EditCustomerViewModel model, CancellationToken cancellationToken)
        {
            var areas = await _lookupService.GetAreasAsync(cancellationToken);
            model.Areas = areas.Select(a => new SelectListItem { Value = a.Id.ToString(), Text = a.Name, Selected = a.Id == model.AreaID }).ToList();

            var subAreas = model.AreaID.HasValue
                ? await _lookupService.GetSubAreasAsync(model.AreaID.Value, cancellationToken)
                : new System.Collections.Generic.List<DTOs.Common.LookupItemDto>();

            model.SubAreas = subAreas.Select(sa => new SelectListItem { Value = sa.Id.ToString(), Text = sa.Name, Selected = sa.Id == model.SubAreaID }).ToList();
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (claim != null && int.TryParse(claim.Value, out int userId))
            {
                return userId;
            }
            return 1; // System Admin default fallback
        }
    }
}
