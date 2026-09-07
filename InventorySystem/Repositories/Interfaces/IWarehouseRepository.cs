using System.Threading;
using System.Threading.Tasks;
using InventorySystem.DTOs.Common;
using InventorySystem.DTOs.Warehouses;
using InventorySystem.Models.Entities;

namespace InventorySystem.Repositories.Interfaces
{
    public interface IWarehouseRepository
    {
        Task<PagedResult<Warehouse>> GetPagedAsync(WarehouseFilterDto filter, CancellationToken cancellationToken = default);
        Task<Warehouse?> GetByIdAsync(int warehouseId, CancellationToken cancellationToken = default);
        Task<bool> ExistsByNameAsync(string name, int? excludeWarehouseId = null, CancellationToken cancellationToken = default);
        Task<bool> IsInUseAsync(int warehouseId, CancellationToken cancellationToken = default);
        Task<bool> HasAnyMainAsync(int? excludeWarehouseId = null, CancellationToken cancellationToken = default);
        Task ClearMainFlagsExceptAsync(int? keepWarehouseId, CancellationToken cancellationToken = default);

        Task AddAsync(Warehouse warehouse, CancellationToken cancellationToken = default);
        Task UpdateAsync(Warehouse warehouse, CancellationToken cancellationToken = default);
        Task SoftDeleteAsync(int warehouseId, int userId, CancellationToken cancellationToken = default);
        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
