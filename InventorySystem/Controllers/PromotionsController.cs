using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using InventorySystem.Data;
using InventorySystem.Filters;
using InventorySystem.Helpers;
using InventorySystem.Models.Entities;
using InventorySystem.Services.Interfaces;
using InventorySystem.ViewModels.Promotions;

namespace InventorySystem.Controllers
{
    [Authorize]
    [RequireCompanyScope]
    public class PromotionsController : InventoryController
    {
        private readonly ApplicationDbContext _context;
        private readonly ILookupService _lookupService;
        private readonly ICompanyContext _companyContext;
        private readonly ILogger<PromotionsController> _logger;

        public PromotionsController(
            ApplicationDbContext context,
            ILookupService lookupService,
            ICompanyContext companyContext,
            ILogger<PromotionsController> logger)
        {
            _context = context;
            _lookupService = lookupService;
            _companyContext = companyContext;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            await _companyContext.TryResolveAsync(cancellationToken);

            var query = _context.PromotionCampaigns
                .AsNoTracking()
                .Where(c => !c.IsDeleted);

            if (_companyContext.HasCompany)
            {
                int companyId = _companyContext.CompanyID;
                query = query.Where(c => c.CompanyID == companyId);
            }

            var campaigns = await query
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
            await _companyContext.TryResolveAsync(cancellationToken);

            var campaign = await _context.PromotionCampaigns
                .AsNoTracking()
                .Include(c => c.Company)
                .Include(c => c.CreatedByUser)
                .Include(c => c.PromotionRules)
                    .ThenInclude(r => r.BuyProduct)
                .Include(c => c.PromotionRules)
                    .ThenInclude(r => r.FreeProduct)
                .FirstOrDefaultAsync(c => c.PromotionID == id && !c.IsDeleted, cancellationToken);

            if (campaign == null || CompanyScopeGuards.IsOutOfScope(_companyContext, campaign.CompanyID))
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
                CompanyName = campaign.Company?.CompanyName ?? CompanyScopeGuards.DisplayName(_companyContext),
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
            await _companyContext.TryResolveAsync(cancellationToken);
            var blocked = CompanyScopeGuards.RedirectIfCannotCreate(this, _companyContext);
            if (blocked != null) return blocked;

            var viewModel = new CreatePromotionViewModel
            {
                CompanyName = _companyContext.CompanyName,
                ProductSelectList = await BuildProductSelectListAsync(_companyContext.CompanyID, cancellationToken),
                Rules = new List<PromotionRuleInputViewModel> { new PromotionRuleInputViewModel() }
            };
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreatePromotionViewModel model, CancellationToken cancellationToken)
        {
            await _companyContext.TryResolveAsync(cancellationToken);
            var blocked = CompanyScopeGuards.RedirectIfCannotCreate(this, _companyContext);
            if (blocked != null) return blocked;

            model.CompanyName = _companyContext.CompanyName;
            model.Rules ??= new List<PromotionRuleInputViewModel>();
            model.Rules = model.Rules.Where(r => !r.IsDeleted).ToList();
            foreach (var rule in model.Rules)
            {
                rule.NormalizeFreeReward();
            }

            await ValidateRulesAgainstCompanyAsync(model.Rules, _companyContext.CompanyID, cancellationToken);

            if (!ModelState.IsValid)
            {
                if (model.Rules.Count == 0)
                {
                    model.Rules.Add(new PromotionRuleInputViewModel());
                }
                model.ProductSelectList = await BuildProductSelectListAsync(_companyContext.CompanyID, cancellationToken);
                return View(model);
            }

            int userId = GetCurrentUserId();
            var campaign = new PromotionCampaign
            {
                Name = model.Name.Trim(),
                CompanyID = _companyContext.CompanyID,
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
            await _companyContext.TryResolveAsync(cancellationToken);

            var campaign = await _context.PromotionCampaigns
                .Include(c => c.Company)
                .Include(c => c.PromotionRules)
                .FirstOrDefaultAsync(c => c.PromotionID == id && !c.IsDeleted, cancellationToken);

            if (campaign == null || CompanyScopeGuards.IsOutOfScope(_companyContext, campaign.CompanyID))
            {
                return NotFound();
            }

            var viewModel = new EditPromotionViewModel
            {
                PromotionID = campaign.PromotionID,
                Name = campaign.Name,
                StartDate = campaign.StartDate.Date,
                EndDate = campaign.EndDate.Date,
                IsActive = campaign.IsActive,
                CompanyName = campaign.Company?.CompanyName ?? CompanyScopeGuards.DisplayName(_companyContext),
                ProductSelectList = await BuildProductSelectListAsync(campaign.CompanyID, cancellationToken),
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

            await _companyContext.TryResolveAsync(cancellationToken);

            var campaign = await _context.PromotionCampaigns
                .Include(c => c.Company)
                .Include(c => c.PromotionRules)
                .FirstOrDefaultAsync(c => c.PromotionID == id && !c.IsDeleted, cancellationToken);

            if (campaign == null || CompanyScopeGuards.IsOutOfScope(_companyContext, campaign.CompanyID))
            {
                return NotFound();
            }

            int companyId = campaign.CompanyID;
            model.CompanyName = campaign.Company?.CompanyName ?? CompanyScopeGuards.DisplayName(_companyContext);
            model.Rules ??= new List<PromotionRuleInputViewModel>();
            model.Rules = model.Rules.Where(r => !r.IsDeleted).ToList();
            foreach (var rule in model.Rules)
            {
                rule.NormalizeFreeReward();
            }

            await ValidateRulesAgainstCompanyAsync(model.Rules, companyId, cancellationToken);

            if (!ModelState.IsValid)
            {
                if (model.Rules.Count == 0)
                {
                    model.Rules.Add(new PromotionRuleInputViewModel());
                }
                model.ProductSelectList = await BuildProductSelectListAsync(companyId, cancellationToken);
                return View(model);
            }

            campaign.Name = model.Name.Trim();
            campaign.StartDate = model.StartDate.Date;
            campaign.EndDate = model.EndDate.Date.AddDays(1).AddTicks(-1);
            campaign.IsActive = model.IsActive;
            // CompanyID immutable after create

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
            await _companyContext.TryResolveAsync(cancellationToken);

            var campaign = await _context.PromotionCampaigns
                .FirstOrDefaultAsync(c => c.PromotionID == id && !c.IsDeleted, cancellationToken);

            if (campaign == null || CompanyScopeGuards.IsOutOfScope(_companyContext, campaign.CompanyID))
            {
                return NotFound();
            }

            campaign.IsActive = !campaign.IsActive;
            await _context.SaveChangesAsync(cancellationToken);
            string statusStr = campaign.IsActive ? "enabled" : "disabled";
            TempData["SuccessMessage"] = $"Promotion campaign '{campaign.Name}' has been {statusStr}.";
            return RedirectToAction(nameof(Index));
        }

        /// <summary>Server re-check for BR-047 and scoped products (defense in depth).</summary>
        private async Task ValidateRulesAgainstCompanyAsync(
            IList<PromotionRuleInputViewModel> rules,
            int companyId,
            CancellationToken cancellationToken)
        {
            var productIds = rules
                .Where(r => !r.IsDeleted)
                .SelectMany(r =>
                {
                    var ids = new List<int> { r.BuyProductID };
                    if (!r.IsCustomFreeItem && r.FreeProductID.HasValue && r.FreeProductID.Value > 0)
                    {
                        ids.Add(r.FreeProductID.Value);
                    }
                    return ids;
                })
                .Distinct()
                .ToList();

            if (productIds.Count == 0)
            {
                return;
            }

            var products = await _context.Products
                .AsNoTracking()
                .Where(p => productIds.Contains(p.ProductID) && !p.IsDeleted)
                .Select(p => new { p.ProductID, p.CompanyID, p.ProductName })
                .ToListAsync(cancellationToken);

            var byId = products.ToDictionary(p => p.ProductID);

            for (int i = 0; i < rules.Count; i++)
            {
                var rule = rules[i];
                if (rule.IsDeleted)
                {
                    continue;
                }

                if (!byId.TryGetValue(rule.BuyProductID, out var buy) || buy.CompanyID != companyId)
                {
                    ModelState.AddModelError($"Rules[{i}].BuyProductID", "Buy product must belong to the selected company.");
                    continue;
                }

                if (rule.IsCustomFreeItem)
                {
                    continue;
                }

                if (!rule.FreeProductID.HasValue || !byId.TryGetValue(rule.FreeProductID.Value, out var free))
                {
                    ModelState.AddModelError($"Rules[{i}].FreeRewardSelection", "Select a free product or Other.");
                    continue;
                }

                if (free.CompanyID != companyId || free.CompanyID != buy.CompanyID)
                {
                    ModelState.AddModelError(
                        $"Rules[{i}].FreeRewardSelection",
                        "Free product must belong to the same company as the buy product (BR-047).");
                }
            }
        }

        private async Task<SelectList> BuildProductSelectListAsync(int companyId, CancellationToken cancellationToken)
        {
            var products = await _lookupService.GetProductsByCompanyAsync(companyId, cancellationToken);
            return new SelectList(products, "Id", "Name");
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
    }
}
