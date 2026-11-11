using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using InventorySystem.DTOs.Areas;
using InventorySystem.DTOs.Common;
using InventorySystem.Models.Entities;

namespace InventorySystem.Repositories.Interfaces
{
    public interface IAreaRepository
    {
        Task<PagedResult<Area>> GetPagedAsync(AreaFilterDto filter, CancellationToken cancellationToken = default);
        Task<Area?> GetByIdAsync(int areaId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<Area>> GetActiveAsync(CancellationToken cancellationToken = default);
        Task<bool> ExistsByNameAsync(string name, int? excludeAreaId = null, CancellationToken cancellationToken = default);
        Task<bool> IsInUseAsync(int areaId, CancellationToken cancellationToken = default);
        Task<int> CountSubAreasAsync(int areaId, CancellationToken cancellationToken = default);
        Task AddAsync(Area area, CancellationToken cancellationToken = default);
        Task UpdateAsync(Area area, CancellationToken cancellationToken = default);
        Task SoftDeleteAsync(int areaId, int userId, CancellationToken cancellationToken = default);
        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
