using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace InventorySystem.ViewModels.Categories
{
    public class EditCategoryViewModel
    {
        public int CategoryID { get; set; }

        [Required(ErrorMessage = "Please select a Company.")]
        [Display(Name = "Company")]
        public int CompanyID { get; set; }

        public string CompanyName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter Category Name.")]
        [MaxLength(100, ErrorMessage = "Category Name cannot exceed 100 characters.")]
        [Display(Name = "Category Name")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Active")]
        public bool IsActive { get; set; }

        /// <summary>When true, Company cannot be changed because products are assigned.</summary>
        public bool HasProducts { get; set; }

        public List<SelectListItem> Companies { get; set; } = new List<SelectListItem>();
    }
}
