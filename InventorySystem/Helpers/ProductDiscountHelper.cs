using System;

namespace InventorySystem.Helpers
{
    /// <summary>
    /// Computes optional product-level line discounts.
    /// Percentage: integer 1–30 of line gross. FixedAmount: PKR per packaging unit × Qty, capped at line gross.
    /// </summary>
    public static class ProductDiscountHelper
    {
        public const int MinPercentage = 1;
        public const int MaxPercentage = 30;

        public sealed class ProductDiscountSnapshot
        {
            public string? Type { get; init; }
            public decimal? Value { get; init; }
            public decimal Amount { get; init; }
        }

        /// <summary>
        /// Normalize optional catalog discount. Empty/invalid type clears discount.
        /// </summary>
        public static (bool Success, string? Error, string? Type, decimal? Value) NormalizeCatalogDiscount(
            string? discountType,
            decimal? discountValue)
        {
            if (string.IsNullOrWhiteSpace(discountType))
            {
                return (true, null, null, null);
            }

            string type = discountType.Trim();
            if (string.Equals(type, "Percentage", StringComparison.OrdinalIgnoreCase))
            {
                if (!discountValue.HasValue)
                {
                    return (false, "Product discount percentage is required when type is Percentage.", null, null);
                }

                decimal value = discountValue.Value;
                if (value != Math.Truncate(value) || value < MinPercentage || value > MaxPercentage)
                {
                    return (false, $"Product discount percentage must be a whole number from {MinPercentage} to {MaxPercentage}.", null, null);
                }

                return (true, null, "Percentage", value);
            }

            if (string.Equals(type, "FixedAmount", StringComparison.OrdinalIgnoreCase))
            {
                if (!discountValue.HasValue)
                {
                    return (false, "Product discount amount is required when type is FixedAmount.", null, null);
                }

                if (discountValue.Value < 0m)
                {
                    return (false, "Product discount amount cannot be negative.", null, null);
                }

                if (discountValue.Value == 0m)
                {
                    return (true, null, null, null);
                }

                return (true, null, "FixedAmount", discountValue.Value);
            }

            return (false, "Product discount type must be Percentage or FixedAmount.", null, null);
        }

        /// <summary>
        /// Compute line product discount from snapshotted type/value. FREE / inactive switch → 0.
        /// </summary>
        public static ProductDiscountSnapshot ComputeLineDiscount(
            bool applyProductDiscount,
            string? discountType,
            decimal? discountValue,
            decimal quantity,
            decimal unitPrice)
        {
            if (!applyProductDiscount
                || string.IsNullOrWhiteSpace(discountType)
                || !discountValue.HasValue
                || discountValue.Value <= 0m
                || quantity <= 0m
                || unitPrice < 0m)
            {
                return new ProductDiscountSnapshot { Type = null, Value = null, Amount = 0m };
            }

            decimal lineGross = Math.Round(quantity * unitPrice, 2, MidpointRounding.AwayFromZero);
            if (lineGross <= 0m)
            {
                return new ProductDiscountSnapshot { Type = null, Value = null, Amount = 0m };
            }

            decimal amount;
            string? type;
            decimal? value;

            if (string.Equals(discountType, "Percentage", StringComparison.OrdinalIgnoreCase))
            {
                decimal pct = discountValue.Value;
                if (pct < MinPercentage || pct > MaxPercentage)
                {
                    return new ProductDiscountSnapshot { Type = null, Value = null, Amount = 0m };
                }

                amount = Math.Round(lineGross * (pct / 100m), 2, MidpointRounding.AwayFromZero);
                type = "Percentage";
                value = pct;
            }
            else if (string.Equals(discountType, "FixedAmount", StringComparison.OrdinalIgnoreCase))
            {
                amount = Math.Round(quantity * discountValue.Value, 2, MidpointRounding.AwayFromZero);
                type = "FixedAmount";
                value = discountValue.Value;
            }
            else
            {
                return new ProductDiscountSnapshot { Type = null, Value = null, Amount = 0m };
            }

            if (amount < 0m) amount = 0m;
            if (amount > lineGross) amount = lineGross;

            if (amount <= 0m)
            {
                return new ProductDiscountSnapshot { Type = null, Value = null, Amount = 0m };
            }

            return new ProductDiscountSnapshot
            {
                Type = type,
                Value = value,
                Amount = amount
            };
        }

        /// <summary>Validate Manual invoice percentage (1–30 whole numbers).</summary>
        public static bool IsValidManualPercentage(decimal value)
        {
            return value == Math.Truncate(value) && value >= MinPercentage && value <= MaxPercentage;
        }
    }
}
