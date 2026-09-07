using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace InventorySystem.ViewModels.Products
{
    public class CreateProductViewModel
    {
        [Required(ErrorMessage = "Product Name is required.")]
        [MaxLength(150, ErrorMessage = "Product Name cannot exceed 150 characters.")]
        [Display(Name = "Product Name")]
        public string ProductName { get; set; } = string.Empty;

        [MaxLength(255, ErrorMessage = "Description cannot exceed 255 characters.")]
        [Display(Name = "Description")]
        public string? Description { get; set; }

        [MaxLength(50, ErrorMessage = "SKU cannot exceed 50 characters.")]
        [Display(Name = "SKU Code")]
        public string? SKU { get; set; }

        [MaxLength(100, ErrorMessage = "Barcode cannot exceed 100 characters.")]
        [Display(Name = "Barcode")]
        public string? Barcode { get; set; }

        [Required(ErrorMessage = "Category is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "Please select a valid Category.")]
        [Display(Name = "Category")]
        public int CategoryID { get; set; }

        [Required(ErrorMessage = "Company (Supplier) is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "Please select a valid Company.")]
        [Display(Name = "Company (Supplier)")]
        public int CompanyID { get; set; }

        public string CompanyName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Base Unit is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "Please select a valid Base Unit.")]
        [Display(Name = "Base Inventory Unit")]
        public int BaseUnitID { get; set; }

        [Required(ErrorMessage = "Selling Price is required.")]
        [Range(0, 100000000, ErrorMessage = "Selling Price cannot be negative.")]
        [Display(Name = "Base Selling Price")]
        public decimal BaseSellingPrice { get; set; }

        [Required(ErrorMessage = "Reorder Level is required.")]
        [Range(0, 100000, ErrorMessage = "Reorder Level cannot be negative.")]
        [Display(Name = "Reorder Stock Threshold")]
        public int ReorderLevel { get; set; }

        [Display(Name = "Active Status")]
        public bool IsActive { get; set; } = true;

        public List<ProductUnitInputViewModel> Units { get; set; } = new List<ProductUnitInputViewModel>();

        // Dropdowns for UI binding
        public IEnumerable<SelectListItem> Categories { get; set; } = new List<SelectListItem>();
        public IEnumerable<SelectListItem> Companies { get; set; } = new List<SelectListItem>();
        public IEnumerable<SelectListItem> AvailableUnits { get; set; } = new List<SelectListItem>();
    }
}
