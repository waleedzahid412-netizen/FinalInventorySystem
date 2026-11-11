using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InventorySystem.DTOs.Areas;
using InventorySystem.Mappings;
using InventorySystem.Services.Interfaces;
using InventorySystem.ViewModels.Areas;

namespace InventorySystem.Controllers
{
    [Authorize]
    public class AreasController : InventoryController
    {
        private readonly IAreaService _areaService;

        public AreasController(IAreaService areaService)
        {
            _areaService = areaService;
        }

        [HttpGet]
        public async Task<IActionResult> Index([FromQuery] AreaFilterDto filter, CancellationToken cancellationToken)
        {
            var paged = await _areaService.GetPagedAreasAsync(filter, cancellationToken);
            return View(new AreaListViewModel
            {
                Filter = filter,
                Areas = paged
            });
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(new CreateAreaViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateAreaViewModel model, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var result = await _areaService.CreateAreaAsync(model.ToDto(), GetCurrentUserId(), cancellationToken);
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
            var dto = await _areaService.GetAreaForEditAsync(id, cancellationToken);
            if (dto == null)
            {
                TempData["ErrorMessage"] = "Area not found.";
                return RedirectToAction(nameof(Index));
            }

            bool isInUse = await _areaService.IsInUseAsync(id, cancellationToken);
            return View(dto.ToViewModel(isInUse));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, EditAreaViewModel model, CancellationToken cancellationToken)
        {
            if (id != model.AreaID)
            {
                return BadRequest();
            }

            model.IsInUse = await _areaService.IsInUseAsync(id, cancellationToken);

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var result = await _areaService.UpdateAreaAsync(model.ToDto(), GetCurrentUserId(), cancellationToken);
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
            var result = await _areaService.SoftDeleteAreaAsync(id, GetCurrentUserId(), cancellationToken);

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
                TempData["ErrorMessage"] = result.Errors.FirstOrDefault() ?? "Failed to delete area.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
