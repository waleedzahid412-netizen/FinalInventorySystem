using System.Threading;
using System.Threading.Tasks;
using InventorySystem.DTOs.Common;
using InventorySystem.DTOs.Units;

namespace InventorySystem.Services.Interfaces
{
    public interface IUnitService
    {
        Task<PagedResult<UnitListItemDto>> GetPagedUnitsAsync(UnitFilterDto filter, CancellationToken cancellationToken = default);
        Task<EditUnitDto?> GetUnitForEditAsync(int unitId, CancellationToken cancellationToken = default);
        Task<bool> IsInUseAsync(int unitId, CancellationToken cancellationToken = default);

        Task<OperationResult<int>> CreateUnitAsync(CreateUnitDto dto, int userId, CancellationToken cancellationToken = default);
        Task<OperationResult> UpdateUnitAsync(EditUnitDto dto, int userId, CancellationToken cancellationToken = default);
        Task<OperationResult> SoftDeleteUnitAsync(int unitId, int userId, CancellationToken cancellationToken = default);
    }
}
