using System.ComponentModel.DataAnnotations;

namespace InventorySystem.ViewModels.Products
{
    public class ProductUnitInputViewModel
    {
        public int ProductUnitID { get; set; }

        [Required(ErrorMessage = "Unit is required.")]
        public int UnitID { get; set; }

        public string UnitName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Conversion factor is required.")]
        [Range(0.0001, 1000000, ErrorMessage = "Conversion factor must be greater than 0.")]
        public decimal ConversionToBaseUnit { get; set; } = 1m;

        [Range(0, 100000000, ErrorMessage = "Purchase price cannot be negative.")]
        public decimal? PurchasePrice { get; set; }

        [Range(0, 100000000, ErrorMessage = "Selling price cannot be negative.")]
        public decimal? SellingPrice { get; set; }

        public bool IsDefaultPurchaseUnit { get; set; }
        public bool IsDefaultSalesUnit { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
