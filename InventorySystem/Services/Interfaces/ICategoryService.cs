using System.Threading;
using System.Threading.Tasks;
using InventorySystem.DTOs.Categories;
using InventorySystem.DTOs.Common;

namespace InventorySystem.Services.Interfaces
{
    public interface ICategoryService
    {
        Task<PagedResult<CategoryListItemDto>> GetPagedCategoriesAsync(CategoryFilterDto filter, CancellationToken cancellationToken = default);
        Task<EditCategoryDto?> GetCategoryForEditAsync(int categoryId, CancellationToken cancellationToken = default);
        Task<bool> HasProductsAsync(int categoryId, CancellationToken cancellationToken = default);

        Task<OperationResult<int>> CreateCategoryAsync(CreateCategoryDto dto, int userId, CancellationToken cancellationToken = default);
        Task<OperationResult> UpdateCategoryAsync(EditCategoryDto dto, int userId, CancellationToken cancellationToken = default);
        Task<OperationResult> SoftDeleteCategoryAsync(int categoryId, int userId, CancellationToken cancellationToken = default);
    }
}
