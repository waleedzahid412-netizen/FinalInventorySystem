using System.Threading;
using System.Threading.Tasks;
using InventorySystem.DTOs.Common;
using InventorySystem.DTOs.SubAreas;
using InventorySystem.Models.Entities;

namespace InventorySystem.Repositories.Interfaces
{
    public interface ISubAreaRepository
    {
        Task<PagedResult<SubArea>> GetPagedAsync(SubAreaFilterDto filter, CancellationToken cancellationToken = default);
        Task<SubArea?> GetByIdAsync(int subAreaId, CancellationToken cancellationToken = default);
        Task<bool> ExistsByNameAsync(int areaId, string name, int? excludeSubAreaId = null, CancellationToken cancellationToken = default);
        Task<bool> AreaExistsAsync(int areaId, CancellationToken cancellationToken = default);
        Task<bool> IsInUseAsync(int subAreaId, CancellationToken cancellationToken = default);
        Task AddAsync(SubArea subArea, CancellationToken cancellationToken = default);
        Task UpdateAsync(SubArea subArea, CancellationToken cancellationToken = default);
        Task SoftDeleteAsync(int subAreaId, int userId, CancellationToken cancellationToken = default);
        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
