using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using InventorySystem.DTOs.Warehouses;
using InventorySystem.Mappings;
using InventorySystem.Services.Interfaces;
using InventorySystem.ViewModels.Warehouses;

namespace InventorySystem.Controllers
{
    [Authorize]
    public class WarehousesController : InventoryController
    {
        private readonly IWarehouseService _warehouseService;
        private readonly ILogger<WarehousesController> _logger;

        public WarehousesController(IWarehouseService warehouseService, ILogger<WarehousesController> logger)
        {
            _warehouseService = warehouseService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index([FromQuery] WarehouseFilterDto filter, CancellationToken cancellationToken)
        {
            var pagedWarehouses = await _warehouseService.GetPagedWarehousesAsync(filter, cancellationToken);

            var viewModel = new WarehouseListViewModel
            {
                Filter = filter,
                Warehouses = pagedWarehouses
            };

            return View(viewModel);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(new CreateWarehouseViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateWarehouseViewModel model, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            int userId = GetCurrentUserId();
            var result = await _warehouseService.CreateWarehouseAsync(model.ToDto(), userId, cancellationToken);

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
            var editDto = await _warehouseService.GetWarehouseForEditAsync(id, cancellationToken);
            if (editDto == null)
            {
                TempData["ErrorMessage"] = "Warehouse not found.";
                return RedirectToAction(nameof(Index));
            }

            bool isInUse = await _warehouseService.IsInUseAsync(id, cancellationToken);
            return View(editDto.ToViewModel(isInUse));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, EditWarehouseViewModel model, CancellationToken cancellationToken)
        {
            if (id != model.WarehouseID)
            {
                return BadRequest();
            }

            model.IsInUse = await _warehouseService.IsInUseAsync(id, cancellationToken);

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            int userId = GetCurrentUserId();
            var result = await _warehouseService.UpdateWarehouseAsync(model.ToDto(), userId, cancellationToken);

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
            var existing = await _warehouseService.GetWarehouseForEditAsync(id, cancellationToken);
            if (existing == null)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = "Warehouse not found." });
                }
                return NotFound();
            }

            int userId = GetCurrentUserId();
            var result = await _warehouseService.SoftDeleteWarehouseAsync(id, userId, cancellationToken);

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
                TempData["ErrorMessage"] = result.Errors.FirstOrDefault() ?? "Failed to delete warehouse.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
