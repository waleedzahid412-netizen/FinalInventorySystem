using InventorySystem.DTOs.Units;
using InventorySystem.ViewModels.Units;

namespace InventorySystem.Mappings
{
    public static class UnitMappingExtensions
    {
        public static CreateUnitDto ToDto(this CreateUnitViewModel model)
        {
            return new CreateUnitDto
            {
                UnitName = model.UnitName,
                IsActive = model.IsActive
            };
        }

        public static EditUnitDto ToDto(this EditUnitViewModel model)
        {
            return new EditUnitDto
            {
                UnitID = model.UnitID,
                UnitName = model.UnitName,
                IsActive = model.IsActive
            };
        }

        public static EditUnitViewModel ToViewModel(this EditUnitDto dto, bool isInUse = false)
        {
            return new EditUnitViewModel
            {
                UnitID = dto.UnitID,
                UnitName = dto.UnitName,
                IsActive = dto.IsActive,
                IsInUse = isInUse
            };
        }
    }
}
