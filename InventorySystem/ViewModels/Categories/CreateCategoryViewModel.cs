using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace InventorySystem.ViewModels.Categories
{
    public class CreateCategoryViewModel
    {
        [Required(ErrorMessage = "Please select a Company.")]
        [Display(Name = "Company")]
        public int CompanyID { get; set; }

        [Required(ErrorMessage = "Please enter Category Name.")]
        [MaxLength(100, ErrorMessage = "Category Name cannot exceed 100 characters.")]
        [Display(Name = "Category Name")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;

        public List<SelectListItem> Companies { get; set; } = new List<SelectListItem>();
    }
}
