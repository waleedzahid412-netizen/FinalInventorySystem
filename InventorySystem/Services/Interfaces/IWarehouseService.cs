using System.Threading;
using System.Threading.Tasks;
using InventorySystem.DTOs.Common;
using InventorySystem.DTOs.Warehouses;

namespace InventorySystem.Services.Interfaces
{
    public interface IWarehouseService
    {
        Task<PagedResult<WarehouseListItemDto>> GetPagedWarehousesAsync(WarehouseFilterDto filter, CancellationToken cancellationToken = default);
        Task<EditWarehouseDto?> GetWarehouseForEditAsync(int warehouseId, CancellationToken cancellationToken = default);
        Task<bool> IsInUseAsync(int warehouseId, CancellationToken cancellationToken = default);

        Task<OperationResult<int>> CreateWarehouseAsync(CreateWarehouseDto dto, int userId, CancellationToken cancellationToken = default);
        Task<OperationResult> UpdateWarehouseAsync(EditWarehouseDto dto, int userId, CancellationToken cancellationToken = default);
        Task<OperationResult> SoftDeleteWarehouseAsync(int warehouseId, int userId, CancellationToken cancellationToken = default);
    }
}
