using InventorySystem.DTOs.Common;
using InventorySystem.DTOs.Warehouses;

namespace InventorySystem.ViewModels.Warehouses
{
    public class WarehouseListViewModel
    {
        public WarehouseFilterDto Filter { get; set; } = new WarehouseFilterDto();
        public PagedResult<WarehouseListItemDto> Warehouses { get; set; } = new PagedResult<WarehouseListItemDto>();
    }
}
