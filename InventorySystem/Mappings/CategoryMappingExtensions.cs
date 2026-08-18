using InventorySystem.DTOs.Categories;
using InventorySystem.ViewModels.Categories;

namespace InventorySystem.Mappings
{
    public static class CategoryMappingExtensions
    {
        public static CreateCategoryDto ToDto(this CreateCategoryViewModel model)
        {
            return new CreateCategoryDto
            {
                CompanyID = model.CompanyID,
                Name = model.Name,
                IsActive = model.IsActive
            };
        }

        public static EditCategoryDto ToDto(this EditCategoryViewModel model)
        {
            return new EditCategoryDto
            {
                CategoryID = model.CategoryID,
                CompanyID = model.CompanyID,
                Name = model.Name,
                IsActive = model.IsActive
            };
        }

        public static EditCategoryViewModel ToViewModel(this EditCategoryDto dto, bool hasProducts = false)
        {
            return new EditCategoryViewModel
            {
                CategoryID = dto.CategoryID,
                CompanyID = dto.CompanyID,
                Name = dto.Name,
                IsActive = dto.IsActive,
                HasProducts = hasProducts
            };
        }
    }
}
