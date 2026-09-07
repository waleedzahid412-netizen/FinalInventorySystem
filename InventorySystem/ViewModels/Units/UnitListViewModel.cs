using InventorySystem.DTOs.Common;
using InventorySystem.DTOs.Units;

namespace InventorySystem.ViewModels.Units
{
    public class UnitListViewModel
    {
        public UnitFilterDto Filter { get; set; } = new UnitFilterDto();
        public PagedResult<UnitListItemDto> Units { get; set; } = new PagedResult<UnitListItemDto>();
    }
}
