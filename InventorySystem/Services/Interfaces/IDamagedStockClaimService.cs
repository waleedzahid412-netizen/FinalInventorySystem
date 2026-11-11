using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using InventorySystem.DTOs.Common;
using InventorySystem.DTOs.DamagedStockClaims;

namespace InventorySystem.Services.Interfaces
{
    public interface IDamagedStockClaimService
    {
        Task<(List<DamagedStockClaimListItemDto> Items, int TotalCount)> GetClaimsAsync(
            DamagedStockClaimFilterDto filter,
            CancellationToken cancellationToken = default);

        Task<List<DamagedStockRowDto>> GetDamagedStockAsync(
            int companyId,
            int? warehouseId = null,
            string? searchTerm = null,
            CancellationToken cancellationToken = default);

        Task<DamagedStockClaimDetailDto?> GetClaimDetailAsync(
            int claimId,
            CancellationToken cancellationToken = default);

        Task<OperationResult<int>> CreateClaimAsync(
            int companyId,
            CreateDamagedStockClaimRequest request,
            int userId,
            CancellationToken cancellationToken = default);

        Task<OperationResult> ConfirmSupplierResponseAsync(
            int claimId,
            ConfirmDamagedStockClaimRequest request,
            int userId,
            CancellationToken cancellationToken = default);

        Task<OperationResult> ApplyRejectedDecisionAsync(
            int claimId,
            RejectedStockDecisionRequest request,
            int userId,
            CancellationToken cancellationToken = default);

        Task<OperationResult<int>> AddSettlementAsync(
            int claimId,
            AddClaimSettlementRequest request,
            int userId,
            CancellationToken cancellationToken = default);

        Task<OperationResult> TryCloseClaimAsync(
            int claimId,
            int userId,
            CancellationToken cancellationToken = default);
    }
}
