using InventorySystem.DTOs.Common;
using InventorySystem.DTOs.Companies;

namespace InventorySystem.ViewModels.Companies
{
    public class CompanyListViewModel
    {
        public CompanyFilterDto Filter { get; set; } = new CompanyFilterDto();
        public PagedResult<CompanyListItemDto> Companies { get; set; } = new PagedResult<CompanyListItemDto>();
    }
}
