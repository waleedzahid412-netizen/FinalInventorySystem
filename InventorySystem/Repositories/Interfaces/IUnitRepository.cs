using System.Threading;
using System.Threading.Tasks;
using InventorySystem.DTOs.Common;
using InventorySystem.DTOs.Units;
using InventorySystem.Models.Entities;

namespace InventorySystem.Repositories.Interfaces
{
    public interface IUnitRepository
    {
        Task<PagedResult<Unit>> GetPagedAsync(UnitFilterDto filter, CancellationToken cancellationToken = default);
        Task<Unit?> GetByIdAsync(int unitId, CancellationToken cancellationToken = default);
        Task<bool> ExistsByNameAsync(string unitName, int? excludeUnitId = null, CancellationToken cancellationToken = default);
        Task<bool> IsInUseAsync(int unitId, CancellationToken cancellationToken = default);

        Task AddAsync(Unit unit, CancellationToken cancellationToken = default);
        Task UpdateAsync(Unit unit, CancellationToken cancellationToken = default);
        Task SoftDeleteAsync(int unitId, int userId, CancellationToken cancellationToken = default);
        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
