using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using InventorySystem.DTOs.Areas;
using InventorySystem.DTOs.Common;

namespace InventorySystem.Services.Interfaces
{
    public interface IAreaService
    {
        Task<PagedResult<AreaListItemDto>> GetPagedAreasAsync(AreaFilterDto filter, CancellationToken cancellationToken = default);
        Task<EditAreaDto?> GetAreaForEditAsync(int areaId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<AreaOptionDto>> GetActiveAreaOptionsAsync(CancellationToken cancellationToken = default);
        Task<bool> IsInUseAsync(int areaId, CancellationToken cancellationToken = default);
        Task<OperationResult<int>> CreateAreaAsync(CreateAreaDto dto, int userId, CancellationToken cancellationToken = default);
        Task<OperationResult> UpdateAreaAsync(EditAreaDto dto, int userId, CancellationToken cancellationToken = default);
        Task<OperationResult> SoftDeleteAreaAsync(int areaId, int userId, CancellationToken cancellationToken = default);
    }
}
