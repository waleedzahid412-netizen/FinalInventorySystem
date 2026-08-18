using System;

namespace InventorySystem.Helpers
{
    /// <summary>
    /// Normalizes dual-unit sales return input (packaging vs base) into packaging Quantity + base ConvertedQuantity.
    /// </summary>
    public static class ReturnQuantityHelper
    {
        public const string PackagingMode = "Packaging";
        public const string BaseMode = "Base";

        public static bool IsBaseMode(string? returnUnitMode)
        {
            if (string.IsNullOrWhiteSpace(returnUnitMode))
            {
                return false;
            }

            return string.Equals(returnUnitMode, BaseMode, StringComparison.OrdinalIgnoreCase)
                || string.Equals(returnUnitMode, "Piece", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Converts entered qty + mode into packaging and base quantities.
        /// Quantity &lt;= 0 yields zeros (line not returned). Positive qty must be a whole number.
        /// </summary>
        public static bool TryNormalize(
            decimal enteredQuantity,
            string? returnUnitMode,
            decimal conversionFactor,
            out decimal packagingQuantity,
            out decimal baseQuantity,
            out string? error)
        {
            packagingQuantity = 0m;
            baseQuantity = 0m;
            error = null;

            if (enteredQuantity <= 0m)
            {
                return true;
            }

            if (enteredQuantity != Math.Truncate(enteredQuantity))
            {
                error = "Return quantity must be a whole number. Use base-unit mode for partial packaging amounts.";
                return false;
            }

            decimal factor = conversionFactor > 0m ? conversionFactor : 1m;

            if (IsBaseMode(returnUnitMode))
            {
                baseQuantity = enteredQuantity;
                packagingQuantity = UnitConversionHelper.ToDisplayUnits(enteredQuantity, factor);
            }
            else
            {
                packagingQuantity = enteredQuantity;
                baseQuantity = UnitConversionHelper.ToBaseUnits(enteredQuantity, factor);
            }

            return true;
        }
    }
}
