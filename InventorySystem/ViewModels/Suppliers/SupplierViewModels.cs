using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using InventorySystem.DTOs.Common;
using InventorySystem.DTOs.Suppliers;

namespace InventorySystem.ViewModels.Suppliers
{
    public class SupplierListViewModel
    {
        public SupplierFilterDto Filter { get; set; } = new SupplierFilterDto();
        public PagedResult<SupplierListItemDto> Suppliers { get; set; } = new PagedResult<SupplierListItemDto>(new List<SupplierListItemDto>(), 0, 1, 10);
    }

    public class CreateSupplierViewModel
    {
        [Required(ErrorMessage = "Supplier name is required.")]
        [MaxLength(100)]
        [Display(Name = "Supplier Name")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "CNIC is required.")]
        [RegularExpression(@"^\d{5}-\d{7}-\d$", ErrorMessage = "CNIC must be in format 12345-1234567-1.")]
        [MaxLength(15)]
        [Display(Name = "CNIC")]
        public string CNIC { get; set; } = string.Empty;

        [MaxLength(20)]
        [Display(Name = "Phone")]
        public string? Phone { get; set; }

        [MaxLength(255)]
        [Display(Name = "Address")]
        public string? Address { get; set; }

        [Required]
        [Display(Name = "Type")]
        public string Type { get; set; } = "Employee";

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;
    }

    public class EditSupplierViewModel
    {
        public int SupplierID { get; set; }

        [Required(ErrorMessage = "Supplier name is required.")]
        [MaxLength(100)]
        [Display(Name = "Supplier Name")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "CNIC is required.")]
        [RegularExpression(@"^\d{5}-\d{7}-\d$", ErrorMessage = "CNIC must be in format 12345-1234567-1.")]
        [MaxLength(15)]
        [Display(Name = "CNIC")]
        public string CNIC { get; set; } = string.Empty;

        [MaxLength(20)]
        [Display(Name = "Phone")]
        public string? Phone { get; set; }

        [MaxLength(255)]
        [Display(Name = "Address")]
        public string? Address { get; set; }

        [Required]
        [Display(Name = "Type")]
        public string Type { get; set; } = "Employee";

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;
    }
}
