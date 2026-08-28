using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace InventorySystem.ViewModels.Discounts
{
    public class DiscountListViewModel
    {
        public int DiscountRuleID { get; set; }
        public string RuleName { get; set; } = string.Empty;
        public decimal MinimumOrderAmount { get; set; }
        public decimal? MaximumOrderAmount { get; set; }
        public string DiscountType { get; set; } = "Percentage";
        public decimal DiscountValue { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public bool IsActive { get; set; }
        public int Priority { get; set; }
    }

    public class DiscountDetailsViewModel
    {
        public int DiscountRuleID { get; set; }
        public string RuleName { get; set; } = string.Empty;
        public decimal MinimumOrderAmount { get; set; }
        public decimal? MaximumOrderAmount { get; set; }
        public string DiscountType { get; set; } = "Percentage";
        public decimal DiscountValue { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public bool IsActive { get; set; }
        public int Priority { get; set; }
        public string CreatedByName { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class CreateDiscountViewModel : IValidatableObject
    {
        public string CompanyName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Rule name is required.")]
        [StringLength(100, ErrorMessage = "Rule name cannot exceed 100 characters.")]
        public string RuleName { get; set; } = string.Empty;

        [Range(0, double.MaxValue, ErrorMessage = "Minimum order amount must be non-negative.")]
        public decimal MinimumOrderAmount { get; set; } = 0m;

        [Range(0, double.MaxValue, ErrorMessage = "Maximum order amount must be non-negative.")]
        public decimal? MaximumOrderAmount { get; set; }

        [Required(ErrorMessage = "Discount type is required.")]
        public string DiscountType { get; set; } = "Percentage"; // "Percentage" or "FixedAmount"

        [Range(0.01, double.MaxValue, ErrorMessage = "Discount value must be greater than 0.")]
        public decimal DiscountValue { get; set; }

        [DataType(DataType.Date)]
        public DateTime? StartDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime? EndDate { get; set; }

        public bool IsActive { get; set; } = true;

        [Range(1, int.MaxValue, ErrorMessage = "Priority must be at least 1.")]
        public int Priority { get; set; } = 1;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (MaximumOrderAmount.HasValue && MaximumOrderAmount.Value < MinimumOrderAmount)
            {
                yield return new ValidationResult("Maximum order amount must be greater than or equal to Minimum order amount.", new[] { nameof(MaximumOrderAmount) });
            }

            if (string.Equals(DiscountType, "Percentage", StringComparison.OrdinalIgnoreCase) && DiscountValue > 100m)
            {
                yield return new ValidationResult("Percentage discount value cannot exceed 100%.", new[] { nameof(DiscountValue) });
            }

            if (StartDate.HasValue && EndDate.HasValue && StartDate.Value > EndDate.Value)
            {
                yield return new ValidationResult("Start date must be less than or equal to End date.", new[] { nameof(StartDate), nameof(EndDate) });
            }
        }
    }

    public class EditDiscountViewModel : IValidatableObject
    {
        public int DiscountRuleID { get; set; }

        public string CompanyName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Rule name is required.")]
        [StringLength(100, ErrorMessage = "Rule name cannot exceed 100 characters.")]
        public string RuleName { get; set; } = string.Empty;

        [Range(0, double.MaxValue, ErrorMessage = "Minimum order amount must be non-negative.")]
        public decimal MinimumOrderAmount { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Maximum order amount must be non-negative.")]
        public decimal? MaximumOrderAmount { get; set; }

        [Required(ErrorMessage = "Discount type is required.")]
        public string DiscountType { get; set; } = "Percentage";

        [Range(0.01, double.MaxValue, ErrorMessage = "Discount value must be greater than 0.")]
        public decimal DiscountValue { get; set; }

        [DataType(DataType.Date)]
        public DateTime? StartDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime? EndDate { get; set; }

        public bool IsActive { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Priority must be at least 1.")]
        public int Priority { get; set; } = 1;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (MaximumOrderAmount.HasValue && MaximumOrderAmount.Value < MinimumOrderAmount)
            {
                yield return new ValidationResult("Maximum order amount must be greater than or equal to Minimum order amount.", new[] { nameof(MaximumOrderAmount) });
            }

            if (string.Equals(DiscountType, "Percentage", StringComparison.OrdinalIgnoreCase) && DiscountValue > 100m)
            {
                yield return new ValidationResult("Percentage discount value cannot exceed 100%.", new[] { nameof(DiscountValue) });
            }

            if (StartDate.HasValue && EndDate.HasValue && StartDate.Value > EndDate.Value)
            {
                yield return new ValidationResult("Start date must be less than or equal to End date.", new[] { nameof(StartDate), nameof(EndDate) });
            }
        }
    }
}
