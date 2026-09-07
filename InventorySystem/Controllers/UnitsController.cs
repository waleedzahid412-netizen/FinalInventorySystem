using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using InventorySystem.DTOs.Units;
using InventorySystem.Mappings;
using InventorySystem.Services.Interfaces;
using InventorySystem.ViewModels.Units;

namespace InventorySystem.Controllers
{
    [Authorize]
    public class UnitsController : InventoryController
    {
        private readonly IUnitService _unitService;
        private readonly ILogger<UnitsController> _logger;

        public UnitsController(IUnitService unitService, ILogger<UnitsController> logger)
        {
            _unitService = unitService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index([FromQuery] UnitFilterDto filter, CancellationToken cancellationToken)
        {
            var pagedUnits = await _unitService.GetPagedUnitsAsync(filter, cancellationToken);

            var viewModel = new UnitListViewModel
            {
                Filter = filter,
                Units = pagedUnits
            };

            return View(viewModel);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(new CreateUnitViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateUnitViewModel model, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            int userId = GetCurrentUserId();
            var result = await _unitService.CreateUnitAsync(model.ToDto(), userId, cancellationToken);

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
            var editDto = await _unitService.GetUnitForEditAsync(id, cancellationToken);
            if (editDto == null)
            {
                TempData["ErrorMessage"] = "Unit not found.";
                return RedirectToAction(nameof(Index));
            }

            bool isInUse = await _unitService.IsInUseAsync(id, cancellationToken);
            return View(editDto.ToViewModel(isInUse));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, EditUnitViewModel model, CancellationToken cancellationToken)
        {
            if (id != model.UnitID)
            {
                return BadRequest();
            }

            model.IsInUse = await _unitService.IsInUseAsync(id, cancellationToken);

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            int userId = GetCurrentUserId();
            var result = await _unitService.UpdateUnitAsync(model.ToDto(), userId, cancellationToken);

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
            var existing = await _unitService.GetUnitForEditAsync(id, cancellationToken);
            if (existing == null)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = "Unit not found." });
                }
                return NotFound();
            }

            int userId = GetCurrentUserId();
            var result = await _unitService.SoftDeleteUnitAsync(id, userId, cancellationToken);

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
                TempData["ErrorMessage"] = result.Errors.FirstOrDefault() ?? "Failed to delete unit.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
