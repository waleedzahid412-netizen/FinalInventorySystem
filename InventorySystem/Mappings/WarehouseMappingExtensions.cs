using InventorySystem.DTOs.Warehouses;
using InventorySystem.ViewModels.Warehouses;

namespace InventorySystem.Mappings
{
    public static class WarehouseMappingExtensions
    {
        public static CreateWarehouseDto ToDto(this CreateWarehouseViewModel model)
        {
            return new CreateWarehouseDto
            {
                Name = model.Name,
                Address = model.Address,
                IsActive = model.IsActive,
                IsMain = model.IsMain
            };
        }

        public static EditWarehouseDto ToDto(this EditWarehouseViewModel model)
        {
            return new EditWarehouseDto
            {
                WarehouseID = model.WarehouseID,
                Name = model.Name,
                Address = model.Address,
                IsActive = model.IsActive,
                IsMain = model.IsMain
            };
        }

        public static EditWarehouseViewModel ToViewModel(this EditWarehouseDto dto, bool isInUse = false)
        {
            return new EditWarehouseViewModel
            {
                WarehouseID = dto.WarehouseID,
                Name = dto.Name,
                Address = dto.Address,
                IsActive = dto.IsActive,
                IsMain = dto.IsMain,
                IsInUse = isInUse
            };
        }
    }
}
