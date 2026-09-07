using System;

namespace InventorySystem.Helpers
{
    /// <summary>
    /// Refund/discount money for dual-unit returns must use base-unit proportions of the
    /// original packaging line total — never Round(base/factor, 3) × UnitPrice, which
    /// mis-prices conversions that do not divide evenly (e.g. 1/13 → 0.077 → 18.02).
    /// </summary>
    public static class ReturnValuationHelper
    {
        /// <summary>
        /// Allocates <paramref name="originalAmount"/> for this return slice using base qty.
        /// Last remaining base slice receives the leftover cents so cumulative never drifts.
        /// </summary>
        public static decimal ComputeBaseProportionalAmount(
            decimal thisBaseQty,
            decimal originalBaseQty,
            decimal originalAmount,
            decimal priorBaseQty,
            decimal? priorAllocatedAmount = null)
        {
            if (thisBaseQty <= 0m || originalBaseQty <= 0m || originalAmount <= 0m)
            {
                return 0m;
            }

            decimal remainingBase = Math.Max(0m, originalBaseQty - priorBaseQty);
            if (remainingBase <= 0m)
            {
                return 0m;
            }

            decimal priorAmount;
            if (priorAllocatedAmount.HasValue)
            {
                priorAmount = Math.Min(Math.Max(0m, priorAllocatedAmount.Value), originalAmount);
            }
            else if (priorBaseQty <= 0m)
            {
                priorAmount = 0m;
            }
            else if (priorBaseQty + 0.0001m >= originalBaseQty)
            {
                priorAmount = originalAmount;
            }
            else
            {
                priorAmount = Math.Round(
                    originalAmount * (priorBaseQty / originalBaseQty),
                    2,
                    MidpointRounding.AwayFromZero);
            }

            decimal remainingAmount = Math.Max(0m, originalAmount - priorAmount);

            if (thisBaseQty + 0.0001m >= remainingBase)
            {
                return remainingAmount;
            }

            decimal thisAmount = Math.Round(
                originalAmount * (thisBaseQty / originalBaseQty),
                2,
                MidpointRounding.AwayFromZero);

            return Math.Min(thisAmount, remainingAmount);
        }

        /// <summary>
        /// Historical NORMAL line gross: Quantity × UnitPrice (packaging unit price snapshot).
        /// </summary>
        public static decimal ComputeOriginalLineTotal(decimal packagingQuantity, decimal unitPrice)
        {
            if (packagingQuantity <= 0m || unitPrice <= 0m)
            {
                return 0m;
            }

            return Math.Round(packagingQuantity * unitPrice, 2, MidpointRounding.AwayFromZero);
        }
    }
}
