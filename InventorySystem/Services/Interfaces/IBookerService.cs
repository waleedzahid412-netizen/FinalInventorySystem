using System.Threading;
using System.Threading.Tasks;
using InventorySystem.DTOs.Bookers;
using InventorySystem.DTOs.Common;

namespace InventorySystem.Services.Interfaces
{
    public interface IBookerService
    {
        Task<PagedResult<BookerListItemDto>> GetPagedBookersAsync(BookerFilterDto filter, CancellationToken cancellationToken = default);
        Task<EditBookerDto?> GetBookerForEditAsync(int bookerId, CancellationToken cancellationToken = default);
        Task<OperationResult<int>> CreateBookerAsync(CreateBookerDto dto, int userId, CancellationToken cancellationToken = default);
        Task<OperationResult> UpdateBookerAsync(EditBookerDto dto, int userId, CancellationToken cancellationToken = default);
        Task<OperationResult> SoftDeleteBookerAsync(int bookerId, int userId, CancellationToken cancellationToken = default);
    }
}
