using System.ComponentModel.DataAnnotations;
using InventorySystem.DTOs.Areas;
using InventorySystem.DTOs.Common;

namespace InventorySystem.ViewModels.Areas
{
    public class AreaListViewModel
    {
        public AreaFilterDto Filter { get; set; } = new AreaFilterDto();
        public PagedResult<AreaListItemDto> Areas { get; set; } = new PagedResult<AreaListItemDto>();
    }

    public class CreateAreaViewModel
    {
        [Required(ErrorMessage = "Area name is required.")]
        [MaxLength(100, ErrorMessage = "Area name cannot exceed 100 characters.")]
        [Display(Name = "Area Name")]
        public string AreaName { get; set; } = string.Empty;

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;
    }

    public class EditAreaViewModel
    {
        public int AreaID { get; set; }

        [Required(ErrorMessage = "Area name is required.")]
        [MaxLength(100, ErrorMessage = "Area name cannot exceed 100 characters.")]
        [Display(Name = "Area Name")]
        public string AreaName { get; set; } = string.Empty;

        [Display(Name = "Active")]
        public bool IsActive { get; set; }

        public bool IsInUse { get; set; }
    }
}
