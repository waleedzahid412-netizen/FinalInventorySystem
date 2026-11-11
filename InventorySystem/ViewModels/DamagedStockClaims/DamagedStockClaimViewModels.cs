using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Rendering;
using InventorySystem.DTOs.DamagedStockClaims;

namespace InventorySystem.ViewModels.DamagedStockClaims
{
    public class DamagedStockClaimIndexViewModel
    {
        public DamagedStockClaimFilterDto Filter { get; set; } = new();
        public List<DamagedStockClaimListItemDto> Claims { get; set; } = new();
        public int TotalCount { get; set; }
        public string ScopeLabel { get; set; } = string.Empty;
        public List<SelectListItem> ClaimStatuses { get; set; } = new();
        public List<SelectListItem> SettlementStatuses { get; set; } = new();
    }

    public class DamagedStockClaimCreateViewModel
    {
        public string CompanyName { get; set; } = string.Empty;
        public int CompanyID { get; set; }
        public List<DamagedStockRowDto> DamagedStock { get; set; } = new();
        public List<SelectListItem> Warehouses { get; set; } = new();
        public int? WarehouseID { get; set; }
        public string? SearchTerm { get; set; }
    }

    public class DamagedStockClaimDetailsViewModel
    {
        public DamagedStockClaimDetailDto Claim { get; set; } = new();
        public List<SelectListItem> Warehouses { get; set; } = new();
        public List<SelectListItem> CompanyProducts { get; set; } = new();
        public List<SelectListItem> PaymentMethods { get; set; } = new()
        {
            new("Cash", "Cash"),
            new("Bank", "Bank"),
            new("Cheque", "Cheque")
        };
    }
}
