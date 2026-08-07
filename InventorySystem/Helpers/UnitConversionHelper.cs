using System;

namespace InventorySystem.Helpers
{
    /// <summary>
    /// Centralized single source of truth for unit conversion calculations.
    /// BR-005, BR-010, BR-012: Converts purchased/sold quantities into Base Units.
    /// </summary>
    public static class UnitConversionHelper
    {
        /// <summary>
        /// Converts a quantity from any unit into Base Units.
        /// ConvertedQuantity = Quantity * ProductUnit.ConversionToBaseUnit
        /// </summary>
        public static decimal ToBaseUnits(decimal quantity, decimal conversionFactor)
        {
            if (quantity <= 0) return 0m;
            if (conversionFactor <= 0) return quantity;

            return Math.Round(quantity * conversionFactor, 3, MidpointRounding.AwayFromZero);
        }

        public static decimal ToDisplayUnits(decimal baseQuantity, decimal conversionFactor)
        {
            if (baseQuantity <= 0) return 0m;
            if (conversionFactor <= 0) return baseQuantity;

            return Math.Round(baseQuantity / conversionFactor, 3, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// Formats a clean conversion preview string for UI components and API responses.
        /// Example: "10 Cartons = 240 Pieces"
        /// </summary>
        public static string FormatConversionPreview(decimal quantity, string unitName, decimal convertedQuantity, string baseUnitName)
        {
            if (string.Equals(unitName, baseUnitName, StringComparison.OrdinalIgnoreCase))
            {
                return $"{quantity:G29} {unitName}";
            }

            return $"{quantity:G29} {unitName} = {convertedQuantity:G29} {baseUnitName}";
        }
    }
}
