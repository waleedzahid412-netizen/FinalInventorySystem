using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Rendering;
using InventorySystem.DTOs.Common;
using InventorySystem.DTOs.Products;

namespace InventorySystem.ViewModels.Products
{
    public class ProductListViewModel
    {
        public ProductFilterDto Filter { get; set; } = new ProductFilterDto();
        public PagedResult<ProductListItemDto> Products { get; set; } = new PagedResult<ProductListItemDto>();

        // Select lists for dropdown filters
        public IEnumerable<SelectListItem> Categories { get; set; } = new List<SelectListItem>();
        public IEnumerable<SelectListItem> Companies { get; set; } = new List<SelectListItem>();
        public IEnumerable<SelectListItem> StatusOptions { get; set; } = new List<SelectListItem>
        {
            new SelectListItem { Value = "", Text = "All Statuses" },
            new SelectListItem { Value = "true", Text = "Active Only" },
            new SelectListItem { Value = "false", Text = "Inactive Only" }
        };
    }
}
