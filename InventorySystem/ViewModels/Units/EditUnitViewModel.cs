using System.ComponentModel.DataAnnotations;

namespace InventorySystem.ViewModels.Units
{
    public class EditUnitViewModel
    {
        public int UnitID { get; set; }

        [Required(ErrorMessage = "Please enter Unit Name.")]
        [MaxLength(50, ErrorMessage = "Unit Name cannot exceed 50 characters.")]
        [Display(Name = "Unit Name")]
        public string UnitName { get; set; } = string.Empty;

        [Display(Name = "Active")]
        public bool IsActive { get; set; }

        /// <summary>When true, unit is referenced by products and soft-delete is blocked.</summary>
        public bool IsInUse { get; set; }
    }
}
