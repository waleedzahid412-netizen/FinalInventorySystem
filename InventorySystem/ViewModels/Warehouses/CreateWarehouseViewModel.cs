using System.ComponentModel.DataAnnotations;

namespace InventorySystem.ViewModels.Warehouses
{
    public class CreateWarehouseViewModel
    {
        [Required(ErrorMessage = "Please enter Warehouse Name.")]
        [MaxLength(100, ErrorMessage = "Warehouse Name cannot exceed 100 characters.")]
        [Display(Name = "Warehouse Name")]
        public string Name { get; set; } = string.Empty;

        [MaxLength(255, ErrorMessage = "Address cannot exceed 255 characters.")]
        [Display(Name = "Address")]
        public string? Address { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "Main Warehouse")]
        public bool IsMain { get; set; }
    }
}
