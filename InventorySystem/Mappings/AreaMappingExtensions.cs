using InventorySystem.DTOs.Areas;
using InventorySystem.ViewModels.Areas;

namespace InventorySystem.Mappings
{
    public static class AreaMappingExtensions
    {
        public static CreateAreaDto ToDto(this CreateAreaViewModel model)
        {
            return new CreateAreaDto
            {
                AreaName = model.AreaName,
                IsActive = model.IsActive
            };
        }

        public static EditAreaDto ToDto(this EditAreaViewModel model)
        {
            return new EditAreaDto
            {
                AreaID = model.AreaID,
                AreaName = model.AreaName,
                IsActive = model.IsActive
            };
        }

        public static EditAreaViewModel ToViewModel(this EditAreaDto dto, bool isInUse = false)
        {
            return new EditAreaViewModel
            {
                AreaID = dto.AreaID,
                AreaName = dto.AreaName,
                IsActive = dto.IsActive,
                IsInUse = isInUse
            };
        }
    }
}
