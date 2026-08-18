using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Rendering;
using InventorySystem.DTOs.Categories;
using InventorySystem.DTOs.Common;

namespace InventorySystem.ViewModels.Categories
{
    public class CategoryListViewModel
    {
        public CategoryFilterDto Filter { get; set; } = new CategoryFilterDto();
        public PagedResult<CategoryListItemDto> Categories { get; set; } = new PagedResult<CategoryListItemDto>();
        public List<SelectListItem> Companies { get; set; } = new List<SelectListItem>();
    }
}
