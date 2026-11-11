using InventorySystem.DTOs.SubAreas;
using InventorySystem.ViewModels.SubAreas;

namespace InventorySystem.Mappings
{
    public static class SubAreaMappingExtensions
    {
        public static CreateSubAreaDto ToDto(this CreateSubAreaViewModel model)
        {
            return new CreateSubAreaDto
            {
                AreaID = model.AreaID,
                SubAreaName = model.SubAreaName,
                IsActive = model.IsActive
            };
        }

        public static EditSubAreaDto ToDto(this EditSubAreaViewModel model)
        {
            return new EditSubAreaDto
            {
                SubAreaID = model.SubAreaID,
                AreaID = model.AreaID,
                SubAreaName = model.SubAreaName,
                IsActive = model.IsActive
            };
        }

        public static EditSubAreaViewModel ToViewModel(this EditSubAreaDto dto, bool isInUse = false)
        {
            return new EditSubAreaViewModel
            {
                SubAreaID = dto.SubAreaID,
                AreaID = dto.AreaID,
                SubAreaName = dto.SubAreaName,
                IsActive = dto.IsActive,
                IsInUse = isInUse
            };
        }
    }
}
