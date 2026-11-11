using System.Threading;
using System.Threading.Tasks;
using InventorySystem.DTOs.Common;
using InventorySystem.DTOs.SubAreas;

namespace InventorySystem.Services.Interfaces
{
    public interface ISubAreaService
    {
        Task<PagedResult<SubAreaListItemDto>> GetPagedSubAreasAsync(SubAreaFilterDto filter, CancellationToken cancellationToken = default);
        Task<EditSubAreaDto?> GetSubAreaForEditAsync(int subAreaId, CancellationToken cancellationToken = default);
        Task<bool> IsInUseAsync(int subAreaId, CancellationToken cancellationToken = default);
        Task<OperationResult<int>> CreateSubAreaAsync(CreateSubAreaDto dto, int userId, CancellationToken cancellationToken = default);
        Task<OperationResult> UpdateSubAreaAsync(EditSubAreaDto dto, int userId, CancellationToken cancellationToken = default);
        Task<OperationResult> SoftDeleteSubAreaAsync(int subAreaId, int userId, CancellationToken cancellationToken = default);
    }
}
