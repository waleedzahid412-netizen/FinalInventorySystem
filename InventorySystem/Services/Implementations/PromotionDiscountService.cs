using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using InventorySystem.Data;
using InventorySystem.DTOs.Sales;
using InventorySystem.Helpers;
using InventorySystem.Services.Interfaces;

namespace InventorySystem.Services.Implementations
{
    public class PromotionDiscountService : IPromotionDiscountService
    {
        private readonly ApplicationDbContext _context;

        public PromotionDiscountService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<EvaluationResultDto> EvaluatePromotionsAndDiscountsAsync(OrderContextDto context, CancellationToken cancellationToken = default)
        {
            var result = new EvaluationResultDto();
            var now = DateTime.UtcNow;

            if (context == null || context.Items == null || !context.Items.Any())
            {
                return result;
            }

            // ===== 1. EVALUATE PROMOTIONS (Buy X Get Y Free) =====
            var activeCampaigns = await _context.PromotionCampaigns
                .AsNoTracking()
                .Include(c => c.PromotionRules)
                    .ThenInclude(r => r.BuyProduct)
                .Include(c => c.PromotionRules)
                    .ThenInclude(r => r.FreeProduct)
                .Where(c => c.IsActive && !c.IsDeleted && c.StartDate <= now && c.EndDate >= now)
                .ToListAsync(cancellationToken);

            // Fetch conversion factors for all cart items
            var cartProductUnitIds = context.Items.Select(i => i.ProductUnitID).Distinct().ToList();
            var productUnits = await _context.ProductUnits
                .AsNoTracking()
                .Where(pu => cartProductUnitIds.Contains(pu.ProductUnitID))
                .ToDictionaryAsync(pu => pu.ProductUnitID, pu => pu.ConversionToBaseUnit, cancellationToken);

            // Calculate total base units per product in cart
            var cartProductBaseQuantities = new Dictionary<int, decimal>();
            foreach (var item in context.Items.Where(i => i.ItemType != "FREE"))
            {
                decimal conv = productUnits.TryGetValue(item.ProductUnitID, out decimal cVal) ? cVal : 1m;
                decimal baseQty = UnitConversionHelper.ToBaseUnits(item.Quantity, conv);

                if (!cartProductBaseQuantities.ContainsKey(item.ProductID))
                {
                    cartProductBaseQuantities[item.ProductID] = 0m;
                }
                cartProductBaseQuantities[item.ProductID] += baseQty;
            }

            foreach (var campaign in activeCampaigns)
            {
                foreach (var rule in campaign.PromotionRules)
                {
                    if (rule.BuyQuantity <= 0 || rule.FreeQuantity <= 0) continue;

                    if (cartProductBaseQuantities.TryGetValue(rule.BuyProductID, out decimal totalBuyBaseQty))
                    {
                        if (totalBuyBaseQty >= rule.BuyQuantity)
                        {
                            int multiplier = (int)Math.Floor(totalBuyBaseQty / rule.BuyQuantity);
                            int freeQty = multiplier * rule.FreeQuantity;

                            // Fetch free product's default unit
                            var freeUnit = await _context.ProductUnits
                                .AsNoTracking()
                                .Include(pu => pu.Unit)
                                .Where(pu => pu.ProductID == rule.FreeProductID && !pu.IsDeleted && pu.IsActive)
                                .OrderByDescending(pu => pu.IsDefaultSalesUnit)
                                .ThenBy(pu => pu.ProductUnitID)
                                .FirstOrDefaultAsync(cancellationToken);

                            int freeUnitId = freeUnit?.ProductUnitID ?? 0;
                            string freeUnitName = freeUnit?.Unit?.UnitName ?? "Unit";

                            result.Promotions.Add(new PromotionSuggestionDto
                            {
                                PromotionID = campaign.PromotionID,
                                RuleID = rule.RuleID,
                                Title = campaign.Name,
                                BuyProductID = rule.BuyProductID,
                                BuyProductName = rule.BuyProduct.ProductName,
                                RuleBuyQuantity = rule.BuyQuantity,
                                FreeProductID = rule.FreeProductID,
                                FreeProductName = rule.FreeProduct.ProductName,
                                RuleFreeQuantity = rule.FreeQuantity,
                                RewardQuantity = freeQty,
                                FreeUnitID = freeUnitId,
                                FreeUnitName = freeUnitName,
                                Message = $"{campaign.Name} (Buy {rule.BuyQuantity} Get {rule.FreeQuantity} Free) — Current Cart Qualifies for {freeQty} FREE {freeUnitName} ({rule.FreeProduct.ProductName})!"
                            });
                        }
                    }
                }
            }

            // ===== 2. EVALUATE ORDER DISCOUNTS =====
            var activeDiscountRules = await _context.DiscountRules
                .AsNoTracking()
                .Where(r => r.IsActive && !r.IsDeleted &&
                            (r.StartDate == null || r.StartDate <= now) &&
                            (r.EndDate == null || r.EndDate >= now))
                .OrderBy(r => r.Priority)
                .ThenByDescending(r => r.MinimumOrderAmount)
                .ToListAsync(cancellationToken);

            foreach (var rule in activeDiscountRules)
            {
                if (context.SubTotal >= rule.MinimumOrderAmount)
                {
                    if (!rule.MaximumOrderAmount.HasValue || context.SubTotal <= rule.MaximumOrderAmount.Value)
                    {
                        decimal discountAmount = 0m;
                        if (string.Equals(rule.DiscountType, "Percentage", StringComparison.OrdinalIgnoreCase))
                        {
                            discountAmount = context.SubTotal * (rule.DiscountValue / 100m);
                        }
                        else
                        {
                            discountAmount = rule.DiscountValue;
                        }

                        discountAmount = Math.Min(context.SubTotal, discountAmount);

                        result.Discounts.Add(new DiscountSuggestionDto
                        {
                            DiscountRuleID = rule.DiscountRuleID,
                            RuleName = rule.RuleName,
                            MinimumOrderAmount = rule.MinimumOrderAmount,
                            DiscountType = rule.DiscountType,
                            DiscountValue = rule.DiscountValue,
                            CalculatedDiscountAmount = Math.Round(discountAmount, 2),
                            Message = $"Order Discount Eligible: {rule.RuleName} (Save PKR {discountAmount:N2})"
                        });
                    }
                }
            }

            return result;
        }
    }
}
