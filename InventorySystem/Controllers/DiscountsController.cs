using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using InventorySystem.Data;
using InventorySystem.Models.Entities;
using InventorySystem.ViewModels.Discounts;

namespace InventorySystem.Controllers
{
    [Authorize]
    public class DiscountsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<DiscountsController> _logger;

        public DiscountsController(
            ApplicationDbContext context,
            ILogger<DiscountsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: Discounts
        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            var rules = await _context.DiscountRules
                .AsNoTracking()
                .Where(r => !r.IsDeleted)
                .OrderBy(r => r.Priority)
                .ThenByDescending(r => r.MinimumOrderAmount)
                .Select(r => new DiscountListViewModel
                {
                    DiscountRuleID = r.DiscountRuleID,
                    RuleName = r.RuleName,
                    MinimumOrderAmount = r.MinimumOrderAmount,
                    MaximumOrderAmount = r.MaximumOrderAmount,
                    DiscountType = r.DiscountType,
                    DiscountValue = r.DiscountValue,
                    StartDate = r.StartDate,
                    EndDate = r.EndDate,
                    IsActive = r.IsActive,
                    Priority = r.Priority
                })
                .ToListAsync(cancellationToken);

            return View(rules);
        }

        // GET: Discounts/Details/5
        [HttpGet]
        public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
        {
            var rule = await _context.DiscountRules
                .AsNoTracking()
                .Include(r => r.CreatedByUser)
                .FirstOrDefaultAsync(r => r.DiscountRuleID == id && !r.IsDeleted, cancellationToken);

            if (rule == null)
            {
                return NotFound();
            }

            var viewModel = new DiscountDetailsViewModel
            {
                DiscountRuleID = rule.DiscountRuleID,
                RuleName = rule.RuleName,
                MinimumOrderAmount = rule.MinimumOrderAmount,
                MaximumOrderAmount = rule.MaximumOrderAmount,
                DiscountType = rule.DiscountType,
                DiscountValue = rule.DiscountValue,
                StartDate = rule.StartDate,
                EndDate = rule.EndDate,
                IsActive = rule.IsActive,
                Priority = rule.Priority,
                CreatedByName = rule.CreatedByUser != null ? rule.CreatedByUser.FullName : "System",
                CreatedAt = rule.CreatedAt
            };

            return View(viewModel);
        }

        // GET: Discounts/Create
        [HttpGet]
        public IActionResult Create()
        {
            var viewModel = new CreateDiscountViewModel();
            return View(viewModel);
        }

        // POST: Discounts/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateDiscountViewModel model, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            int userId = GetUserId();

            var rule = new DiscountRule
            {
                RuleName = model.RuleName.Trim(),
                MinimumOrderAmount = model.MinimumOrderAmount,
                MaximumOrderAmount = model.MaximumOrderAmount,
                DiscountType = model.DiscountType,
                DiscountValue = model.DiscountValue,
                StartDate = model.StartDate?.Date,
                EndDate = model.EndDate?.Date.AddDays(1).AddTicks(-1),
                IsActive = model.IsActive,
                Priority = model.Priority,
                CreatedBy = userId,
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false
            };

            _context.DiscountRules.Add(rule);
            await _context.SaveChangesAsync(cancellationToken);

            TempData["SuccessMessage"] = $"Discount rule '{rule.RuleName}' created successfully.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Discounts/Edit/5
        [HttpGet]
        public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
        {
            var rule = await _context.DiscountRules
                .FirstOrDefaultAsync(r => r.DiscountRuleID == id && !r.IsDeleted, cancellationToken);

            if (rule == null)
            {
                return NotFound();
            }

            var viewModel = new EditDiscountViewModel
            {
                DiscountRuleID = rule.DiscountRuleID,
                RuleName = rule.RuleName,
                MinimumOrderAmount = rule.MinimumOrderAmount,
                MaximumOrderAmount = rule.MaximumOrderAmount,
                DiscountType = rule.DiscountType,
                DiscountValue = rule.DiscountValue,
                StartDate = rule.StartDate?.Date,
                EndDate = rule.EndDate?.Date,
                IsActive = rule.IsActive,
                Priority = rule.Priority
            };

            return View(viewModel);
        }

        // POST: Discounts/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, EditDiscountViewModel model, CancellationToken cancellationToken)
        {
            if (id != model.DiscountRuleID)
            {
                return BadRequest();
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var rule = await _context.DiscountRules
                .FirstOrDefaultAsync(r => r.DiscountRuleID == id && !r.IsDeleted, cancellationToken);

            if (rule == null)
            {
                return NotFound();
            }

            rule.RuleName = model.RuleName.Trim();
            rule.MinimumOrderAmount = model.MinimumOrderAmount;
            rule.MaximumOrderAmount = model.MaximumOrderAmount;
            rule.DiscountType = model.DiscountType;
            rule.DiscountValue = model.DiscountValue;
            rule.StartDate = model.StartDate?.Date;
            rule.EndDate = model.EndDate?.Date.AddDays(1).AddTicks(-1);
            rule.IsActive = model.IsActive;
            rule.Priority = model.Priority;

            await _context.SaveChangesAsync(cancellationToken);

            TempData["SuccessMessage"] = $"Discount rule '{rule.RuleName}' updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        // POST: Discounts/ToggleStatus/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id, CancellationToken cancellationToken)
        {
            var rule = await _context.DiscountRules
                .FirstOrDefaultAsync(r => r.DiscountRuleID == id && !r.IsDeleted, cancellationToken);

            if (rule == null)
            {
                return NotFound();
            }

            rule.IsActive = !rule.IsActive;
            await _context.SaveChangesAsync(cancellationToken);

            string statusStr = rule.IsActive ? "enabled" : "disabled";
            TempData["SuccessMessage"] = $"Discount rule '{rule.RuleName}' has been {statusStr}.";
            return RedirectToAction(nameof(Index));
        }

        private int GetUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (claim != null && int.TryParse(claim.Value, out int userId))
            {
                return userId;
            }
            return 1; // Default fallback admin user
        }
    }
}
