using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InventorySystem.DTOs.Suppliers;
using InventorySystem.Mappings;
using InventorySystem.Services.Interfaces;
using InventorySystem.ViewModels.Suppliers;

namespace InventorySystem.Controllers
{
    [Authorize]
    public class SuppliersController : InventoryController
    {
        private readonly ISupplierService _supplierService;

        public SuppliersController(ISupplierService supplierService)
        {
            _supplierService = supplierService;
        }

        [HttpGet]
        public async Task<IActionResult> Index([FromQuery] SupplierFilterDto filter, CancellationToken cancellationToken)
        {
            var paged = await _supplierService.GetPagedSuppliersAsync(filter, cancellationToken);
            return View(new SupplierListViewModel { Filter = filter, Suppliers = paged });
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(new CreateSupplierViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateSupplierViewModel model, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var result = await _supplierService.CreateSupplierAsync(model.ToDto(), GetCurrentUserId(), cancellationToken);
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
            var dto = await _supplierService.GetSupplierForEditAsync(id, cancellationToken);
            if (dto == null)
            {
                TempData["ErrorMessage"] = "Supplier not found.";
                return RedirectToAction(nameof(Index));
            }

            return View(dto.ToViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, EditSupplierViewModel model, CancellationToken cancellationToken)
        {
            if (id != model.SupplierID)
            {
                return BadRequest();
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var result = await _supplierService.UpdateSupplierAsync(model.ToDto(), GetCurrentUserId(), cancellationToken);
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
            var result = await _supplierService.SoftDeleteSupplierAsync(id, GetCurrentUserId(), cancellationToken);

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
                TempData["ErrorMessage"] = result.Errors.FirstOrDefault() ?? "Failed to delete supplier.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
