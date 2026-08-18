using System;
using System.Collections.Generic;
using System.Linq;

namespace InventorySystem.Helpers
{
    /// <summary>
    /// Allocates an invoice-level discount across NORMAL line items at sale time.
    /// Sum of allocated amounts always equals headerDiscount (last line receives remainder).
    /// FREE lines are excluded by the caller.
    /// </summary>
    public static class DiscountAllocationHelper
    {
        public sealed class LineGross
        {
            public int Index { get; init; }
            public decimal Gross { get; init; }
        }

        public sealed class LineAllocation
        {
            public int Index { get; init; }
            public decimal DiscountAmount { get; init; }
            public decimal DiscountRate { get; init; }
        }

        /// <summary>
        /// Compute header discount amount from type/value and paid subtotal.
        /// </summary>
        public static decimal ComputeHeaderDiscount(string discountType, decimal discountValue, decimal paidSubTotal)
        {
            if (paidSubTotal <= 0m || discountValue <= 0m)
            {
                return 0m;
            }

            decimal amount;
            if (string.Equals(discountType, "Percentage", StringComparison.OrdinalIgnoreCase))
            {
                amount = paidSubTotal * (discountValue / 100m);
            }
            else
            {
                // FixedAmount
                amount = discountValue;
            }

            return Math.Min(paidSubTotal, Math.Round(amount, 2, MidpointRounding.AwayFromZero));
        }

        /// <summary>
        /// Allocate headerDiscount across NORMAL line gross amounts.
        /// Percentage: rate copied to each line; amounts rounded with remainder on last line.
        /// FixedAmount: proportional to line gross; rate stored as 0.
        /// </summary>
        public static List<LineAllocation> Allocate(
            IReadOnlyList<LineGross> normalLines,
            decimal headerDiscount,
            string discountType,
            decimal discountValue)
        {
            var result = new List<LineAllocation>();
            if (normalLines == null || normalLines.Count == 0 || headerDiscount <= 0m)
            {
                if (normalLines != null)
                {
                    foreach (var line in normalLines)
                    {
                        result.Add(new LineAllocation { Index = line.Index, DiscountAmount = 0m, DiscountRate = 0m });
                    }
                }
                return result;
            }

            decimal subtotal = normalLines.Sum(l => l.Gross);
            if (subtotal <= 0m)
            {
                foreach (var line in normalLines)
                {
                    result.Add(new LineAllocation { Index = line.Index, DiscountAmount = 0m, DiscountRate = 0m });
                }
                return result;
            }

            bool isPercentage = string.Equals(discountType, "Percentage", StringComparison.OrdinalIgnoreCase);
            decimal rate = isPercentage ? discountValue : 0m;

            decimal allocatedSum = 0m;
            for (int i = 0; i < normalLines.Count; i++)
            {
                var line = normalLines[i];
                decimal amount;
                if (i == normalLines.Count - 1)
                {
                    amount = Math.Round(headerDiscount - allocatedSum, 2, MidpointRounding.AwayFromZero);
                }
                else if (isPercentage)
                {
                    amount = Math.Round(line.Gross * (discountValue / 100m), 2, MidpointRounding.AwayFromZero);
                    allocatedSum += amount;
                }
                else
                {
                    amount = Math.Round(headerDiscount * (line.Gross / subtotal), 2, MidpointRounding.AwayFromZero);
                    allocatedSum += amount;
                }

                if (amount < 0m) amount = 0m;
                if (amount > line.Gross) amount = line.Gross;

                result.Add(new LineAllocation
                {
                    Index = line.Index,
                    DiscountAmount = amount,
                    DiscountRate = isPercentage ? rate : 0m
                });
            }

            // Reconcile last line if earlier clamps broke the sum
            decimal finalSum = result.Sum(r => r.DiscountAmount);
            if (finalSum != headerDiscount && result.Count > 0)
            {
                var last = result[result.Count - 1];
                decimal adjusted = Math.Round(last.DiscountAmount + (headerDiscount - finalSum), 2, MidpointRounding.AwayFromZero);
                var lastGross = normalLines[normalLines.Count - 1].Gross;
                if (adjusted < 0m) adjusted = 0m;
                if (adjusted > lastGross) adjusted = lastGross;
                result[result.Count - 1] = new LineAllocation
                {
                    Index = last.Index,
                    DiscountAmount = adjusted,
                    DiscountRate = last.DiscountRate
                };
            }

            return result;
        }
    }
}
