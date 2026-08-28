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
using InventorySystem.Filters;
using InventorySystem.Helpers;
using InventorySystem.Models.Entities;
using InventorySystem.Services.Interfaces;
using InventorySystem.ViewModels.Discounts;

namespace InventorySystem.Controllers
{
    [Authorize]
    [RequireCompanyScope]
    public class DiscountsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ICompanyContext _companyContext;
        private readonly ILogger<DiscountsController> _logger;

        public DiscountsController(
            ApplicationDbContext context,
            ICompanyContext companyContext,
            ILogger<DiscountsController> logger)
        {
            _context = context;
            _companyContext = companyContext;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            await _companyContext.TryResolveAsync(cancellationToken);

            var query = _context.DiscountRules
                .AsNoTracking()
                .Where(r => !r.IsDeleted);

            if (_companyContext.HasCompany)
            {
                int companyId = _companyContext.CompanyID;
                query = query.Where(r => r.CompanyID == companyId);
            }

            var rules = await query
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

        [HttpGet]
        public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
        {
            await _companyContext.TryResolveAsync(cancellationToken);

            var rule = await _context.DiscountRules
                .AsNoTracking()
                .Include(r => r.CreatedByUser)
                .Include(r => r.Company)
                .FirstOrDefaultAsync(r => r.DiscountRuleID == id && !r.IsDeleted, cancellationToken);

            if (rule == null || CompanyScopeGuards.IsOutOfScope(_companyContext, rule.CompanyID))
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
                CompanyName = rule.Company?.CompanyName ?? CompanyScopeGuards.DisplayName(_companyContext),
                CreatedByName = rule.CreatedByUser != null ? rule.CreatedByUser.FullName : "System",
                CreatedAt = rule.CreatedAt
            };

            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> Create(CancellationToken cancellationToken)
        {
            await _companyContext.TryResolveAsync(cancellationToken);
            var blocked = CompanyScopeGuards.RedirectIfCannotCreate(this, _companyContext);
            if (blocked != null) return blocked;

            return View(new CreateDiscountViewModel { CompanyName = _companyContext.CompanyName });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateDiscountViewModel model, CancellationToken cancellationToken)
        {
            await _companyContext.TryResolveAsync(cancellationToken);
            var blocked = CompanyScopeGuards.RedirectIfCannotCreate(this, _companyContext);
            if (blocked != null) return blocked;

            model.CompanyName = _companyContext.CompanyName;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            int userId = GetUserId();

            var rule = new DiscountRule
            {
                RuleName = model.RuleName.Trim(),
                CompanyID = _companyContext.CompanyID,
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

        [HttpGet]
        public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
        {
            await _companyContext.TryResolveAsync(cancellationToken);

            var rule = await _context.DiscountRules
                .Include(r => r.Company)
                .FirstOrDefaultAsync(r => r.DiscountRuleID == id && !r.IsDeleted, cancellationToken);

            if (rule == null || CompanyScopeGuards.IsOutOfScope(_companyContext, rule.CompanyID))
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
                Priority = rule.Priority,
                CompanyName = rule.Company?.CompanyName ?? CompanyScopeGuards.DisplayName(_companyContext)
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, EditDiscountViewModel model, CancellationToken cancellationToken)
        {
            if (id != model.DiscountRuleID)
            {
                return BadRequest();
            }

            await _companyContext.TryResolveAsync(cancellationToken);

            var rule = await _context.DiscountRules
                .Include(r => r.Company)
                .FirstOrDefaultAsync(r => r.DiscountRuleID == id && !r.IsDeleted, cancellationToken);

            if (rule == null || CompanyScopeGuards.IsOutOfScope(_companyContext, rule.CompanyID))
            {
                return NotFound();
            }

            model.CompanyName = rule.Company?.CompanyName ?? CompanyScopeGuards.DisplayName(_companyContext);

            if (!ModelState.IsValid)
            {
                return View(model);
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
            // CompanyID immutable after create

            await _context.SaveChangesAsync(cancellationToken);

            TempData["SuccessMessage"] = $"Discount rule '{rule.RuleName}' updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id, CancellationToken cancellationToken)
        {
            await _companyContext.TryResolveAsync(cancellationToken);

            var rule = await _context.DiscountRules
                .FirstOrDefaultAsync(r => r.DiscountRuleID == id && !r.IsDeleted, cancellationToken);

            if (rule == null || CompanyScopeGuards.IsOutOfScope(_companyContext, rule.CompanyID))
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
            return 1;
        }
    }
}
