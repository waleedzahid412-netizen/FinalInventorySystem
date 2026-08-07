using System;
using System.Collections.Generic;
using InventorySystem.DTOs.Common;

namespace InventorySystem.DTOs.Products
{
    public class ProductListItemDto
    {
        public int ProductID { get; set; }
        public string ProductCode => $"PRD-{ProductID:D5}";
        public string ProductName { get; set; } = string.Empty;
        public string? SKU { get; set; }
        public string? Barcode { get; set; }
        public int CategoryID { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public int CompanyID { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public string BaseUnitName { get; set; } = string.Empty;
        public decimal BaseSellingPrice { get; set; }
        public decimal AveragePurchaseCost { get; set; }
        public decimal CurrentStock { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class ProductUnitDto
    {
        public int ProductUnitID { get; set; }
        public int UnitID { get; set; }
        public string UnitName { get; set; } = string.Empty;
        public decimal ConversionToBaseUnit { get; set; }
        public decimal? PurchasePrice { get; set; }
        public decimal? SellingPrice { get; set; }
        public bool IsDefaultPurchaseUnit { get; set; }
        public bool IsDefaultSalesUnit { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class CreateProductDto
    {
        public string ProductName { get; set; } = string.Empty;
        public string? SKU { get; set; }
        public string? Barcode { get; set; }
        public int CategoryID { get; set; }
        public int CompanyID { get; set; }
        public int BaseUnitID { get; set; }
        public decimal BaseSellingPrice { get; set; }
        public decimal AveragePurchaseCost { get; set; }
        public int ReorderLevel { get; set; }
        public bool IsActive { get; set; } = true;

        public List<ProductUnitDto> Units { get; set; } = new List<ProductUnitDto>();
    }

    public class EditProductDto
    {
        public int ProductID { get; set; }
        public string ProductCode => $"PRD-{ProductID:D5}";
        public string ProductName { get; set; } = string.Empty;
        public string? SKU { get; set; }
        public string? Barcode { get; set; }
        public int CategoryID { get; set; }
        public int CompanyID { get; set; }
        public int BaseUnitID { get; set; }
        public decimal BaseSellingPrice { get; set; }
        public decimal AveragePurchaseCost { get; set; }
        public int ReorderLevel { get; set; }
        public bool IsActive { get; set; }

        public List<ProductUnitDto> Units { get; set; } = new List<ProductUnitDto>();
    }

    public class ProductDetailsDto
    {
        public int ProductID { get; set; }
        public string ProductCode => $"PRD-{ProductID:D5}";
        public string ProductName { get; set; } = string.Empty;
        public string? SKU { get; set; }
        public string? Barcode { get; set; }
        public int CategoryID { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public int CompanyID { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public int BaseUnitID { get; set; }
        public string BaseUnitName { get; set; } = string.Empty;
        public decimal BaseSellingPrice { get; set; }
        public decimal AveragePurchaseCost { get; set; }
        public int ReorderLevel { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }

        public InventorySummaryDto? InventorySummary { get; set; }
        public List<WarehouseStockSummaryDto> WarehouseStocks { get; set; } = new List<WarehouseStockSummaryDto>();
        public List<ProductUnitDto> ProductUnits { get; set; } = new List<ProductUnitDto>();
    }

    public class ProductFormDropdownsDto
    {
        public List<LookupItemDto> Categories { get; set; } = new List<LookupItemDto>();
        public List<LookupItemDto> Companies { get; set; } = new List<LookupItemDto>();
        public List<LookupItemDto> Units { get; set; } = new List<LookupItemDto>();
    }
}
