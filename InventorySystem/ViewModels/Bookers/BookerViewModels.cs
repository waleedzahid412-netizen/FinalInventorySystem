using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using InventorySystem.DTOs.Bookers;
using InventorySystem.DTOs.Common;

namespace InventorySystem.ViewModels.Bookers
{
    public class BookerListViewModel
    {
        public BookerFilterDto Filter { get; set; } = new BookerFilterDto();
        public PagedResult<BookerListItemDto> Bookers { get; set; } = new PagedResult<BookerListItemDto>(new List<BookerListItemDto>(), 0, 1, 10);
    }

    public class CreateBookerViewModel
    {
        [Display(Name = "Company")]
        public string CompanyName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Booker name is required.")]
        [MaxLength(100)]
        [Display(Name = "Booker Name")]
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

        [Range(0, double.MaxValue, ErrorMessage = "Credit limit cannot be negative.")]
        [Display(Name = "Credit Limit")]
        public decimal CreditLimit { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;
    }

    public class EditBookerViewModel
    {
        public int BookerID { get; set; }

        [Display(Name = "Company")]
        public string CompanyName { get; set; } = string.Empty;

        public bool CanChangeCompany { get; set; }

        public decimal OutstandingBalance { get; set; }

        [Required(ErrorMessage = "Booker name is required.")]
        [MaxLength(100)]
        [Display(Name = "Booker Name")]
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

        [Range(0, double.MaxValue, ErrorMessage = "Credit limit cannot be negative.")]
        [Display(Name = "Credit Limit")]
        public decimal CreditLimit { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;
    }
}
