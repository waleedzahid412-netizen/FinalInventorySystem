using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using InventorySystem.DTOs.Common;
using InventorySystem.DTOs.SubAreas;

namespace InventorySystem.ViewModels.SubAreas
{
    public class SubAreaListViewModel
    {
        public SubAreaFilterDto Filter { get; set; } = new SubAreaFilterDto();
        public PagedResult<SubAreaListItemDto> SubAreas { get; set; } = new PagedResult<SubAreaListItemDto>();
        public IEnumerable<SelectListItem> AreaOptions { get; set; } = new List<SelectListItem>();
    }

    public class CreateSubAreaViewModel
    {
        [Range(1, int.MaxValue, ErrorMessage = "Parent area is required.")]
        [Display(Name = "Area")]
        public int AreaID { get; set; }

        [Required(ErrorMessage = "Sub area name is required.")]
        [MaxLength(100, ErrorMessage = "Sub area name cannot exceed 100 characters.")]
        [Display(Name = "Sub Area Name")]
        public string SubAreaName { get; set; } = string.Empty;

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;

        public IEnumerable<SelectListItem> AreaOptions { get; set; } = new List<SelectListItem>();
    }

    public class EditSubAreaViewModel
    {
        public int SubAreaID { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Parent area is required.")]
        [Display(Name = "Area")]
        public int AreaID { get; set; }

        [Required(ErrorMessage = "Sub area name is required.")]
        [MaxLength(100, ErrorMessage = "Sub area name cannot exceed 100 characters.")]
        [Display(Name = "Sub Area Name")]
        public string SubAreaName { get; set; } = string.Empty;

        [Display(Name = "Active")]
        public bool IsActive { get; set; }

        public bool IsInUse { get; set; }

        public IEnumerable<SelectListItem> AreaOptions { get; set; } = new List<SelectListItem>();
    }
}
