using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using InventorySystem.Data;
using InventorySystem.Models.Entities;
using InventorySystem.Services.Interfaces;
using InventorySystem.ViewModels.Promotions;

namespace InventorySystem.Controllers
{
    [Authorize]
    public class PromotionsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILookupService _lookupService;
        private readonly ILogger<PromotionsController> _logger;

        public PromotionsController(
            ApplicationDbContext context,
            ILookupService lookupService,
            ILogger<PromotionsController> logger)
        {
            _context = context;
            _lookupService = lookupService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            var campaigns = await _context.PromotionCampaigns
                .AsNoTracking()
                .Where(c => !c.IsDeleted)
                .Include(c => c.PromotionRules)
                .OrderByDescending(c => c.PromotionID)
                .Select(c => new PromotionListViewModel
                {
                    PromotionID = c.PromotionID,
                    Name = c.Name,
                    StartDate = c.StartDate,
                    EndDate = c.EndDate,
                    IsActive = c.IsActive,
                    RuleCount = c.PromotionRules.Count
                })
                .ToListAsync(cancellationToken);

            return View(campaigns);
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
        {
            var campaign = await _context.PromotionCampaigns
                .AsNoTracking()
                .Include(c => c.CreatedByUser)
                .Include(c => c.PromotionRules)
                    .ThenInclude(r => r.BuyProduct)
                .Include(c => c.PromotionRules)
                    .ThenInclude(r => r.FreeProduct)
                .FirstOrDefaultAsync(c => c.PromotionID == id && !c.IsDeleted, cancellationToken);

            if (campaign == null)
            {
                return NotFound();
            }

            var viewModel = new PromotionDetailsViewModel
            {
                PromotionID = campaign.PromotionID,
                Name = campaign.Name,
                StartDate = campaign.StartDate,
                EndDate = campaign.EndDate,
                IsActive = campaign.IsActive,
                CreatedByName = campaign.CreatedByUser != null ? campaign.CreatedByUser.FullName : "System",
                Rules = campaign.PromotionRules.Select(r => new PromotionRuleItemViewModel
                {
                    RuleID = r.RuleID,
                    BuyProductID = r.BuyProductID,
                    BuyProductName = r.BuyProduct.ProductName,
                    BuyQuantity = r.BuyQuantity,
                    FreeProductID = r.FreeProductID,
                    FreeProductName = r.IsCustomFreeItem
                        ? $"Other: {r.CustomFreeItemName}"
                        : (r.FreeProduct?.ProductName ?? string.Empty),
                    IsCustomFreeItem = r.IsCustomFreeItem,
                    CustomFreeItemName = r.CustomFreeItemName,
                    FreeQuantity = r.FreeQuantity
                }).ToList()
            };

            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> Create(CancellationToken cancellationToken)
        {
            var products = await _lookupService.GetProductsAsync(cancellationToken);
            var viewModel = new CreatePromotionViewModel
            {
                ProductSelectList = new SelectList(products, "Id", "Name"),
                Rules = new List<PromotionRuleInputViewModel> { new PromotionRuleInputViewModel() }
            };
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreatePromotionViewModel model, CancellationToken cancellationToken)
        {
            model.Rules ??= new List<PromotionRuleInputViewModel>();
            model.Rules = model.Rules.Where(r => !r.IsDeleted).ToList();
            foreach (var rule in model.Rules)
            {
                rule.NormalizeFreeReward();
            }

            if (!ModelState.IsValid)
            {
                if (model.Rules.Count == 0)
                {
                    model.Rules.Add(new PromotionRuleInputViewModel());
                }
                var products = await _lookupService.GetProductsAsync(cancellationToken);
                model.ProductSelectList = new SelectList(products, "Id", "Name");
                return View(model);
            }

            int userId = GetUserId();
            var campaign = new PromotionCampaign
            {
                Name = model.Name.Trim(),
                StartDate = model.StartDate.Date,
                EndDate = model.EndDate.Date.AddDays(1).AddTicks(-1),
                IsActive = model.IsActive,
                CreatedBy = userId,
                IsDeleted = false
            };

            foreach (var ruleInput in model.Rules)
            {
                ruleInput.NormalizeFreeReward();
                campaign.PromotionRules.Add(MapRule(ruleInput));
            }

            _context.PromotionCampaigns.Add(campaign);
            await _context.SaveChangesAsync(cancellationToken);
            TempData["SuccessMessage"] = $"Promotion campaign '{campaign.Name}' created successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
        {
            var campaign = await _context.PromotionCampaigns
                .Include(c => c.PromotionRules)
                .FirstOrDefaultAsync(c => c.PromotionID == id && !c.IsDeleted, cancellationToken);

            if (campaign == null)
            {
                return NotFound();
            }

            var products = await _lookupService.GetProductsAsync(cancellationToken);
            var viewModel = new EditPromotionViewModel
            {
                PromotionID = campaign.PromotionID,
                Name = campaign.Name,
                StartDate = campaign.StartDate.Date,
                EndDate = campaign.EndDate.Date,
                IsActive = campaign.IsActive,
                ProductSelectList = new SelectList(products, "Id", "Name"),
                Rules = campaign.PromotionRules.Select(r => new PromotionRuleInputViewModel
                {
                    RuleID = r.RuleID,
                    BuyProductID = r.BuyProductID,
                    BuyQuantity = r.BuyQuantity,
                    FreeProductID = r.FreeProductID,
                    IsCustomFreeItem = r.IsCustomFreeItem,
                    CustomFreeItemName = r.CustomFreeItemName,
                    FreeRewardSelection = r.IsCustomFreeItem
                        ? PromotionRuleInputViewModel.OtherRewardValue
                        : (r.FreeProductID?.ToString() ?? string.Empty),
                    FreeQuantity = r.FreeQuantity
                }).ToList()
            };

            if (viewModel.Rules.Count == 0)
            {
                viewModel.Rules.Add(new PromotionRuleInputViewModel());
            }

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, EditPromotionViewModel model, CancellationToken cancellationToken)
        {
            if (id != model.PromotionID)
            {
                return BadRequest();
            }

            model.Rules ??= new List<PromotionRuleInputViewModel>();
            model.Rules = model.Rules.Where(r => !r.IsDeleted).ToList();
            foreach (var rule in model.Rules)
            {
                rule.NormalizeFreeReward();
            }

            if (!ModelState.IsValid)
            {
                if (model.Rules.Count == 0)
                {
                    model.Rules.Add(new PromotionRuleInputViewModel());
                }
                var products = await _lookupService.GetProductsAsync(cancellationToken);
                model.ProductSelectList = new SelectList(products, "Id", "Name");
                return View(model);
            }

            var campaign = await _context.PromotionCampaigns
                .Include(c => c.PromotionRules)
                .FirstOrDefaultAsync(c => c.PromotionID == id && !c.IsDeleted, cancellationToken);

            if (campaign == null)
            {
                return NotFound();
            }

            campaign.Name = model.Name.Trim();
            campaign.StartDate = model.StartDate.Date;
            campaign.EndDate = model.EndDate.Date.AddDays(1).AddTicks(-1);
            campaign.IsActive = model.IsActive;

            var inputRuleIds = model.Rules.Where(r => r.RuleID > 0).Select(r => r.RuleID).ToList();
            var rulesToRemove = campaign.PromotionRules.Where(r => !inputRuleIds.Contains(r.RuleID)).ToList();
            foreach (var rule in rulesToRemove)
            {
                _context.PromotionRules.Remove(rule);
            }

            foreach (var ruleInput in model.Rules)
            {
                ruleInput.NormalizeFreeReward();
                if (ruleInput.RuleID > 0)
                {
                    var existingRule = campaign.PromotionRules.FirstOrDefault(r => r.RuleID == ruleInput.RuleID);
                    if (existingRule != null)
                    {
                        ApplyRuleInput(existingRule, ruleInput);
                    }
                }
                else
                {
                    campaign.PromotionRules.Add(MapRule(ruleInput));
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
            TempData["SuccessMessage"] = $"Promotion campaign '{campaign.Name}' updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id, CancellationToken cancellationToken)
        {
            var campaign = await _context.PromotionCampaigns
                .FirstOrDefaultAsync(c => c.PromotionID == id && !c.IsDeleted, cancellationToken);

            if (campaign == null)
            {
                return NotFound();
            }

            campaign.IsActive = !campaign.IsActive;
            await _context.SaveChangesAsync(cancellationToken);
            string statusStr = campaign.IsActive ? "enabled" : "disabled";
            TempData["SuccessMessage"] = $"Promotion campaign '{campaign.Name}' has been {statusStr}.";
            return RedirectToAction(nameof(Index));
        }

        private static PromotionRule MapRule(PromotionRuleInputViewModel ruleInput) => new PromotionRule
        {
            BuyProductID = ruleInput.BuyProductID,
            BuyQuantity = ruleInput.BuyQuantity,
            FreeProductID = ruleInput.IsCustomFreeItem ? null : ruleInput.FreeProductID,
            IsCustomFreeItem = ruleInput.IsCustomFreeItem,
            CustomFreeItemName = ruleInput.IsCustomFreeItem ? ruleInput.CustomFreeItemName : null,
            FreeQuantity = ruleInput.FreeQuantity
        };

        private static void ApplyRuleInput(PromotionRule existingRule, PromotionRuleInputViewModel ruleInput)
        {
            existingRule.BuyProductID = ruleInput.BuyProductID;
            existingRule.BuyQuantity = ruleInput.BuyQuantity;
            existingRule.FreeProductID = ruleInput.IsCustomFreeItem ? null : ruleInput.FreeProductID;
            existingRule.IsCustomFreeItem = ruleInput.IsCustomFreeItem;
            existingRule.CustomFreeItemName = ruleInput.IsCustomFreeItem ? ruleInput.CustomFreeItemName : null;
            existingRule.FreeQuantity = ruleInput.FreeQuantity;
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
