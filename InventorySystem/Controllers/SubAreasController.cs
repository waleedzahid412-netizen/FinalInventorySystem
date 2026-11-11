using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using InventorySystem.DTOs.SubAreas;
using InventorySystem.Mappings;
using InventorySystem.Services.Interfaces;
using InventorySystem.ViewModels.SubAreas;

namespace InventorySystem.Controllers
{
    [Authorize]
    public class SubAreasController : InventoryController
    {
        private readonly ISubAreaService _subAreaService;
        private readonly IAreaService _areaService;

        public SubAreasController(ISubAreaService subAreaService, IAreaService areaService)
        {
            _subAreaService = subAreaService;
            _areaService = areaService;
        }

        [HttpGet]
        public async Task<IActionResult> Index([FromQuery] SubAreaFilterDto filter, CancellationToken cancellationToken)
        {
            var paged = await _subAreaService.GetPagedSubAreasAsync(filter, cancellationToken);
            return View(new SubAreaListViewModel
            {
                Filter = filter,
                SubAreas = paged,
                AreaOptions = await BuildAreaOptionsAsync(includeEmpty: true, cancellationToken)
            });
        }

        [HttpGet]
        public async Task<IActionResult> Create(CancellationToken cancellationToken)
        {
            var model = new CreateSubAreaViewModel
            {
                AreaOptions = await BuildAreaOptionsAsync(includeEmpty: false, cancellationToken)
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateSubAreaViewModel model, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                model.AreaOptions = await BuildAreaOptionsAsync(includeEmpty: false, cancellationToken);
                return View(model);
            }

            var result = await _subAreaService.CreateSubAreaAsync(model.ToDto(), GetCurrentUserId(), cancellationToken);
            if (!result.Success)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error);
                }
                model.AreaOptions = await BuildAreaOptionsAsync(includeEmpty: false, cancellationToken);
                return View(model);
            }

            TempData["SuccessMessage"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
        {
            var dto = await _subAreaService.GetSubAreaForEditAsync(id, cancellationToken);
            if (dto == null)
            {
                TempData["ErrorMessage"] = "Sub area not found.";
                return RedirectToAction(nameof(Index));
            }

            bool isInUse = await _subAreaService.IsInUseAsync(id, cancellationToken);
            var model = dto.ToViewModel(isInUse);
            model.AreaOptions = await BuildAreaOptionsAsync(includeEmpty: false, cancellationToken, includeAreaId: dto.AreaID);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, EditSubAreaViewModel model, CancellationToken cancellationToken)
        {
            if (id != model.SubAreaID)
            {
                return BadRequest();
            }

            model.IsInUse = await _subAreaService.IsInUseAsync(id, cancellationToken);
            model.AreaOptions = await BuildAreaOptionsAsync(includeEmpty: false, cancellationToken, includeAreaId: model.AreaID);

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var result = await _subAreaService.UpdateSubAreaAsync(model.ToDto(), GetCurrentUserId(), cancellationToken);
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
            var result = await _subAreaService.SoftDeleteSubAreaAsync(id, GetCurrentUserId(), cancellationToken);

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
                TempData["ErrorMessage"] = result.Errors.FirstOrDefault() ?? "Failed to delete sub area.";
            }

            return RedirectToAction(nameof(Index));
        }

        private async Task<IEnumerable<SelectListItem>> BuildAreaOptionsAsync(
            bool includeEmpty,
            CancellationToken cancellationToken,
            int? includeAreaId = null)
        {
            var areas = (await _areaService.GetActiveAreaOptionsAsync(cancellationToken)).ToList();

            if (includeAreaId.HasValue && includeAreaId.Value > 0
                && areas.All(a => a.AreaID != includeAreaId.Value))
            {
                var current = await _areaService.GetAreaForEditAsync(includeAreaId.Value, cancellationToken);
                if (current != null)
                {
                    areas.Add(new InventorySystem.DTOs.Areas.AreaOptionDto
                    {
                        AreaID = current.AreaID,
                        AreaName = current.AreaName + " (inactive)"
                    });
                }
            }

            var items = areas
                .OrderBy(a => a.AreaName)
                .Select(a => new SelectListItem { Value = a.AreaID.ToString(), Text = a.AreaName })
                .ToList();

            if (includeEmpty)
            {
                items.Insert(0, new SelectListItem { Value = "", Text = "All areas" });
            }

            return items;
        }
    }
}
