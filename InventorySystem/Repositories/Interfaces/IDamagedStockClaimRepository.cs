using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using InventorySystem.DTOs.DamagedStockClaims;
using InventorySystem.Models.Entities;

namespace InventorySystem.Repositories.Interfaces
{
    public interface IDamagedStockClaimRepository
    {
        Task<string> GenerateNextClaimNumberAsync(CancellationToken cancellationToken = default);

        Task<(List<DamagedStockClaimListItemDto> Items, int TotalCount)> GetClaimsAsync(
            DamagedStockClaimFilterDto filter,
            CancellationToken cancellationToken = default);

        Task<List<DamagedStockRowDto>> GetDamagedStockAsync(
            int companyId,
            int? warehouseId = null,
            string? searchTerm = null,
            CancellationToken cancellationToken = default);

        Task<DamagedStockClaim?> GetClaimByIdAsync(
            int claimId,
            bool includeDetails = true,
            CancellationToken cancellationToken = default);

        Task<InventoryStock?> GetStockForUpdateAsync(int inventoryStockId, CancellationToken cancellationToken = default);

        Task<decimal> GetSuggestedUnitCostAsync(int productId, int warehouseId, CancellationToken cancellationToken = default);

        Task AddClaimAsync(DamagedStockClaim claim, CancellationToken cancellationToken = default);

        Task AddInventoryTransactionAsync(InventoryTransaction transaction, CancellationToken cancellationToken = default);

        Task AddCompanyLedgerAsync(CompanyLedger ledger, CancellationToken cancellationToken = default);

        Task AddSettlementAsync(DamagedStockClaimSettlement settlement, CancellationToken cancellationToken = default);

        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
