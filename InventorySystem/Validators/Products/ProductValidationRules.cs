using System.Collections.Generic;
using System.Linq;
using InventorySystem.DTOs.Products;
using InventorySystem.ViewModels.Products;

namespace InventorySystem.Validators.Products
{
    public static class ProductValidationRules
    {
        public static List<string> ValidateCreateViewModel(CreateProductViewModel model)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(model.ProductName))
            {
                errors.Add("Product Name is required.");
            }

            if (model.CategoryID <= 0)
            {
                errors.Add("Category selection is required.");
            }

            if (model.BaseUnitID <= 0)
            {
                errors.Add("Base Unit selection is required.");
            }

            if (model.BaseSellingPrice < 0)
            {
                errors.Add("Base Selling Price cannot be negative.");
            }

            if (model.AveragePurchaseCost < 0)
            {
                errors.Add("Purchase Cost cannot be negative.");
            }

            if (model.ReorderLevel < 0)
            {
                errors.Add("Reorder Level cannot be negative.");
            }

            ValidateUnitInputList(model.BaseUnitID, model.Units, errors);

            return errors;
        }

        public static List<string> ValidateEditViewModel(EditProductViewModel model)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(model.ProductName))
            {
                errors.Add("Product Name is required.");
            }

            if (model.CategoryID <= 0)
            {
                errors.Add("Category selection is required.");
            }

            if (model.BaseSellingPrice < 0)
            {
                errors.Add("Base Selling Price cannot be negative.");
            }

            if (model.AveragePurchaseCost < 0)
            {
                errors.Add("Purchase Cost cannot be negative.");
            }

            if (model.ReorderLevel < 0)
            {
                errors.Add("Reorder Level cannot be negative.");
            }

            ValidateUnitInputList(model.BaseUnitID, model.Units, errors);

            return errors;
        }

        private static void ValidateUnitInputList(int baseUnitId, List<ProductUnitInputViewModel> units, List<string> errors)
        {
            if (units == null || !units.Any())
            {
                errors.Add("At least one packaging/selling unit is required.");
                return;
            }

            // Check duplicate units
            var duplicateUnitIds = units.GroupBy(u => u.UnitID).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (duplicateUnitIds.Any())
            {
                errors.Add("Duplicate unit entries detected. Each unit can only be mapped once.");
            }

            // Check conversion factors
            if (units.Any(u => u.ConversionToBaseUnit <= 0))
            {
                errors.Add("Unit conversion factor must be greater than zero.");
            }

            // Check base unit conversion
            var baseUnitMapping = units.FirstOrDefault(u => u.UnitID == baseUnitId);
            if (baseUnitMapping != null && baseUnitMapping.ConversionToBaseUnit != 1m)
            {
                errors.Add("Base Unit conversion factor must equal 1.");
            }

            // Check default purchase unit
            int defaultPurchaseCount = units.Count(u => u.IsDefaultPurchaseUnit);
            if (defaultPurchaseCount != 1)
            {
                errors.Add("Exactly one Default Purchase Unit must be selected.");
            }

            // Check default sales unit
            int defaultSalesCount = units.Count(u => u.IsDefaultSalesUnit);
            if (defaultSalesCount != 1)
            {
                errors.Add("Exactly one Default Sales Unit must be selected.");
            }
        }
    }
}
