using System.Threading;
using System.Threading.Tasks;
using InventorySystem.DTOs.Common;
using InventorySystem.DTOs.LoadSheets;

namespace InventorySystem.Services.Interfaces
{
    public interface ILoadSheetService
    {
        Task<OperationResult<LoadSheetDto>> GenerateLoadSheetAsync(LoadSheetFilterDto filter, CancellationToken cancellationToken = default);
    }
}
