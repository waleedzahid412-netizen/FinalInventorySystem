using System.Threading;
using System.Threading.Tasks;
using InventorySystem.DTOs.Categories;
using InventorySystem.DTOs.Common;
using InventorySystem.Models.Entities;

namespace InventorySystem.Repositories.Interfaces
{
    public interface ICategoryRepository
    {
        Task<PagedResult<Category>> GetPagedAsync(CategoryFilterDto filter, CancellationToken cancellationToken = default);
        Task<Category?> GetByIdAsync(int categoryId, CancellationToken cancellationToken = default);
        Task<bool> ExistsByNameAsync(int companyId, string name, int? excludeCategoryId = null, CancellationToken cancellationToken = default);
        Task<bool> HasProductsAsync(int categoryId, CancellationToken cancellationToken = default);
        Task<bool> CompanyExistsAsync(int companyId, CancellationToken cancellationToken = default);

        Task AddAsync(Category category, CancellationToken cancellationToken = default);
        Task UpdateAsync(Category category, CancellationToken cancellationToken = default);
        Task SoftDeleteAsync(int categoryId, int userId, CancellationToken cancellationToken = default);
        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
