using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using InventorySystem.DTOs.Common;
using InventorySystem.DTOs.Customers;

namespace InventorySystem.ViewModels.Customers
{
    public class CustomerListViewModel
    {
        public CustomerFilterDto Filter { get; set; } = new CustomerFilterDto();
        public PagedResult<CustomerListDto> Customers { get; set; } = new PagedResult<CustomerListDto>();
        public List<SelectListItem> Areas { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> SubAreas { get; set; } = new List<SelectListItem>();
    }

    public class CreateCustomerViewModel
    {
        [Required(ErrorMessage = "Shop Name is required.")]
        [StringLength(150, ErrorMessage = "Shop Name cannot exceed 150 characters.")]
        [Display(Name = "Shop Name")]
        public string ShopName { get; set; } = string.Empty;

        [StringLength(100, ErrorMessage = "Owner Name cannot exceed 100 characters.")]
        [Display(Name = "Owner Name")]
        public string? OwnerName { get; set; }

        [StringLength(20, ErrorMessage = "Phone number cannot exceed 20 characters.")]
        [Display(Name = "Phone Number")]
        public string? Phone { get; set; }

        [StringLength(255, ErrorMessage = "Address cannot exceed 255 characters.")]
        [Display(Name = "Address")]
        public string? Address { get; set; }

        [Display(Name = "Area")]
        public int? AreaID { get; set; }

        [Display(Name = "Sub-Area")]
        public int? SubAreaID { get; set; }

        [StringLength(50, ErrorMessage = "Tax ID / NTN cannot exceed 50 characters.")]
        [Display(Name = "Tax ID / NTN")]
        public string? TaxID { get; set; }

        [Range(0, 100000000, ErrorMessage = "Credit Limit must be a non-negative value.")]
        [Display(Name = "Credit Limit (PKR)")]
        public decimal CreditLimit { get; set; } = 0;

        [Display(Name = "Active Status")]
        public bool IsActive { get; set; } = true;

        public List<SelectListItem> Areas { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> SubAreas { get; set; } = new List<SelectListItem>();
    }

    public class EditCustomerViewModel
    {
        public int CustomerID { get; set; }

        [Required(ErrorMessage = "Shop Name is required.")]
        [StringLength(150, ErrorMessage = "Shop Name cannot exceed 150 characters.")]
        [Display(Name = "Shop Name")]
        public string ShopName { get; set; } = string.Empty;

        [StringLength(100, ErrorMessage = "Owner Name cannot exceed 100 characters.")]
        [Display(Name = "Owner Name")]
        public string? OwnerName { get; set; }

        [StringLength(20, ErrorMessage = "Phone number cannot exceed 20 characters.")]
        [Display(Name = "Phone Number")]
        public string? Phone { get; set; }

        [StringLength(255, ErrorMessage = "Address cannot exceed 255 characters.")]
        [Display(Name = "Address")]
        public string? Address { get; set; }

        [Display(Name = "Area")]
        public int? AreaID { get; set; }

        [Display(Name = "Sub-Area")]
        public int? SubAreaID { get; set; }

        [StringLength(50, ErrorMessage = "Tax ID / NTN cannot exceed 50 characters.")]
        [Display(Name = "Tax ID / NTN")]
        public string? TaxID { get; set; }

        [Range(0, 100000000, ErrorMessage = "Credit Limit must be a non-negative value.")]
        [Display(Name = "Credit Limit (PKR)")]
        public decimal CreditLimit { get; set; } = 0;

        [Display(Name = "Active Status")]
        public bool IsActive { get; set; } = true;

        public List<SelectListItem> Areas { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> SubAreas { get; set; } = new List<SelectListItem>();
    }

    public class CustomerDetailsViewModel
    {
        public CustomerDetailsDto Details { get; set; } = new CustomerDetailsDto();
        public string ActiveTab { get; set; } = "sales";
    }
}
