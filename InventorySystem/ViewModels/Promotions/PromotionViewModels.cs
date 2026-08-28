using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using InventorySystem.Data;

namespace InventorySystem.ViewModels.Promotions
{
    public class PromotionListViewModel
    {
        public int PromotionID { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsActive { get; set; }
        public int RuleCount { get; set; }
    }

    public class PromotionRuleItemViewModel
    {
        public int RuleID { get; set; }
        public int BuyProductID { get; set; }
        public string BuyProductName { get; set; } = string.Empty;
        public int BuyQuantity { get; set; }
        public int? FreeProductID { get; set; }
        public string FreeProductName { get; set; } = string.Empty;
        public bool IsCustomFreeItem { get; set; }
        public string? CustomFreeItemName { get; set; }
        public int FreeQuantity { get; set; }
    }

    public class PromotionDetailsViewModel
    {
        public int PromotionID { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsActive { get; set; }
        public string CreatedByName { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public List<PromotionRuleItemViewModel> Rules { get; set; } = new();
    }

    public class PromotionRuleInputViewModel : IValidatableObject
    {
        public const string OtherRewardValue = "OTHER";

        public int RuleID { get; set; }

        [Required(ErrorMessage = "Buy product is required.")]
        public int BuyProductID { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Buy quantity must be at least 1.")]
        public int BuyQuantity { get; set; } = 1;

        [Required(ErrorMessage = "Free reward is required.")]
        public string FreeRewardSelection { get; set; } = string.Empty;

        public int? FreeProductID { get; set; }

        public bool IsCustomFreeItem { get; set; }

        [StringLength(200, ErrorMessage = "Custom free item name cannot exceed 200 characters.")]
        public string? CustomFreeItemName { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Free quantity must be at least 1.")]
        public int FreeQuantity { get; set; } = 1;

        public bool IsDeleted { get; set; }

        public void NormalizeFreeReward()
        {
            if (string.Equals(FreeRewardSelection, OtherRewardValue, StringComparison.OrdinalIgnoreCase))
            {
                IsCustomFreeItem = true;
                FreeProductID = null;
                CustomFreeItemName = string.IsNullOrWhiteSpace(CustomFreeItemName) ? null : CustomFreeItemName.Trim();
            }
            else if (int.TryParse(FreeRewardSelection, out int productId) && productId > 0)
            {
                IsCustomFreeItem = false;
                FreeProductID = productId;
                CustomFreeItemName = null;
            }
            else
            {
                IsCustomFreeItem = false;
                FreeProductID = null;
            }
        }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (IsDeleted)
            {
                yield break;
            }

            NormalizeFreeReward();

            if (IsCustomFreeItem)
            {
                if (string.IsNullOrWhiteSpace(CustomFreeItemName))
                {
                    yield return new ValidationResult("Enter a name for the custom free item.", new[] { nameof(CustomFreeItemName) });
                }

                yield break;
            }

            if (!FreeProductID.HasValue || FreeProductID.Value <= 0)
            {
                yield return new ValidationResult("Select a free product or Other.", new[] { nameof(FreeRewardSelection) });
                yield break;
            }

            // BR-047: free product must belong to the same company as the buy product.
            var db = validationContext.GetService(typeof(ApplicationDbContext)) as ApplicationDbContext;
            if (db == null)
            {
                yield break;
            }

            var productIds = new[] { BuyProductID, FreeProductID.Value };
            var companies = db.Products
                .AsNoTracking()
                .Where(p => productIds.Contains(p.ProductID) && !p.IsDeleted)
                .Select(p => new { p.ProductID, p.CompanyID })
                .ToList();

            var buy = companies.FirstOrDefault(p => p.ProductID == BuyProductID);
            var free = companies.FirstOrDefault(p => p.ProductID == FreeProductID.Value);
            if (buy == null || free == null)
            {
                yield break;
            }

            if (buy.CompanyID != free.CompanyID)
            {
                yield return new ValidationResult(
                    "Free product must belong to the same company as the buy product (BR-047).",
                    new[] { nameof(FreeRewardSelection) });
            }
        }
    }

    public class CreatePromotionViewModel : IValidatableObject
    {
        [Required(ErrorMessage = "Promotion name is required.")]
        [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Start date is required.")]
        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "End date is required.")]
        [DataType(DataType.Date)]
        public DateTime EndDate { get; set; } = DateTime.Today.AddMonths(1);

        public bool IsActive { get; set; } = true;

        public string CompanyName { get; set; } = string.Empty;

        public List<PromotionRuleInputViewModel> Rules { get; set; } = new();

        public SelectList? ProductSelectList { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (StartDate > EndDate)
            {
                yield return new ValidationResult("Start date must be less than or equal to End date.", new[] { nameof(StartDate), nameof(EndDate) });
            }

            if (Rules == null || !Rules.Exists(r => !r.IsDeleted))
            {
                yield return new ValidationResult("At least one valid promotion rule must be provided.", new[] { nameof(Rules) });
            }
        }
    }

    public class EditPromotionViewModel : IValidatableObject
    {
        public int PromotionID { get; set; }

        [Required(ErrorMessage = "Promotion name is required.")]
        [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Start date is required.")]
        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; }

        [Required(ErrorMessage = "End date is required.")]
        [DataType(DataType.Date)]
        public DateTime EndDate { get; set; }

        public bool IsActive { get; set; }

        public string CompanyName { get; set; } = string.Empty;

        public List<PromotionRuleInputViewModel> Rules { get; set; } = new();

        public SelectList? ProductSelectList { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (StartDate > EndDate)
            {
                yield return new ValidationResult("Start date must be less than or equal to End date.", new[] { nameof(StartDate), nameof(EndDate) });
            }

            if (Rules == null || !Rules.Exists(r => !r.IsDeleted))
            {
                yield return new ValidationResult("At least one valid promotion rule must be provided.", new[] { nameof(Rules) });
            }
        }
    }
}
