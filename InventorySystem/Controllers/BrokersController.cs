using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InventorySystem.DTOs.Brokers;
using InventorySystem.Mappings;
using InventorySystem.Services.Interfaces;
using InventorySystem.ViewModels.Brokers;

namespace InventorySystem.Controllers
{
    [Authorize]
    public class BrokersController : Controller
    {
        private readonly IBrokerService _brokerService;

        public BrokersController(IBrokerService brokerService)
        {
            _brokerService = brokerService;
        }

        [HttpGet]
        public async Task<IActionResult> Index([FromQuery] BrokerFilterDto filter, CancellationToken cancellationToken)
        {
            var paged = await _brokerService.GetPagedBrokersAsync(filter, cancellationToken);
            return View(new BrokerListViewModel { Filter = filter, Brokers = paged });
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(new CreateBrokerViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateBrokerViewModel model, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var result = await _brokerService.CreateBrokerAsync(model.ToDto(), cancellationToken);
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
            var dto = await _brokerService.GetBrokerForEditAsync(id, cancellationToken);
            if (dto == null)
            {
                TempData["ErrorMessage"] = "Broker not found.";
                return RedirectToAction(nameof(Index));
            }

            return View(dto.ToViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, EditBrokerViewModel model, CancellationToken cancellationToken)
        {
            if (id != model.BrokerID)
            {
                return BadRequest();
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var result = await _brokerService.UpdateBrokerAsync(model.ToDto(), cancellationToken);
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
            var result = await _brokerService.SoftDeleteBrokerAsync(id, cancellationToken);

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
                TempData["ErrorMessage"] = result.Errors.FirstOrDefault() ?? "Failed to delete broker.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
