using System.ComponentModel.DataAnnotations;

namespace InventorySystem.ViewModels.Companies
{
    public class EditCompanyViewModel
    {
        public int CompanyID { get; set; }

        [Required(ErrorMessage = "Please enter Company Name.")]
        [MaxLength(150, ErrorMessage = "Company Name cannot exceed 150 characters.")]
        [Display(Name = "Company Name")]
        public string CompanyName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter Contact Person.")]
        [MaxLength(100, ErrorMessage = "Contact Person cannot exceed 100 characters.")]
        [Display(Name = "Contact Person")]
        public string ContactPerson { get; set; } = string.Empty;

        [MaxLength(20, ErrorMessage = "Phone number cannot exceed 20 characters.")]
        [Phone(ErrorMessage = "Please enter a valid phone number.")]
        [Display(Name = "Phone Number")]
        public string? Phone { get; set; }

        [MaxLength(100, ErrorMessage = "Email cannot exceed 100 characters.")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
        [Display(Name = "Email Address")]
        public string? Email { get; set; }

        [MaxLength(255, ErrorMessage = "Address cannot exceed 255 characters.")]
        [Display(Name = "Office / Factory Address")]
        public string? Address { get; set; }

        [MaxLength(50, ErrorMessage = "Tax Number cannot exceed 50 characters.")]
        [Display(Name = "Tax Identification Number (NTN/STRN)")]
        public string? TaxID { get; set; }

        [Range(0, 100000000, ErrorMessage = "Credit Limit cannot be negative.")]
        [Display(Name = "Supplier Credit Limit")]
        public decimal CreditLimit { get; set; }
    }
}
