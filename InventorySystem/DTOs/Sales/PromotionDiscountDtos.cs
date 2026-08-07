using System;
using System.Collections.Generic;

namespace InventorySystem.DTOs.Sales
{
    public class CartItemDto
    {
        public int ProductID { get; set; }
        public int ProductUnitID { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public string ItemType { get; set; } = "NORMAL";
        public int? PromotionID { get; set; }
    }

    public class OrderContextDto
    {
        public int CustomerID { get; set; }
        public int WarehouseID { get; set; }
        public decimal SubTotal { get; set; }
        public List<CartItemDto> Items { get; set; } = new List<CartItemDto>();
    }

    public class PromotionSuggestionDto
    {
        public int PromotionID { get; set; }
        public int RuleID { get; set; }
        public string Title { get; set; } = string.Empty;
        public int BuyProductID { get; set; }
        public string BuyProductName { get; set; } = string.Empty;
        public int RuleBuyQuantity { get; set; }
        public int FreeProductID { get; set; }
        public string FreeProductName { get; set; } = string.Empty;
        public int RuleFreeQuantity { get; set; }
        public int RewardQuantity { get; set; }
        public int FreeUnitID { get; set; }
        public string FreeUnitName { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public class DiscountSuggestionDto
    {
        public int DiscountRuleID { get; set; }
        public string RuleName { get; set; } = string.Empty;
        public decimal MinimumOrderAmount { get; set; }
        public string DiscountType { get; set; } = "Percentage"; // Percentage | FixedAmount
        public decimal DiscountValue { get; set; }
        public decimal CalculatedDiscountAmount { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class EvaluationResultDto
    {
        public List<PromotionSuggestionDto> Promotions { get; set; } = new List<PromotionSuggestionDto>();
        public List<DiscountSuggestionDto> Discounts { get; set; } = new List<DiscountSuggestionDto>();
    }

    public class ProductUnitPriceDto
    {
        public int ProductUnitID { get; set; }
        public int ProductID { get; set; }
        public string UnitName { get; set; } = string.Empty;
        public decimal ConversionToBaseUnit { get; set; }
        public decimal UnitPrice { get; set; }
    }

    public class AvailableStockDto
    {
        public int ProductID { get; set; }
        public int WarehouseID { get; set; }
        public int ProductUnitID { get; set; }
        public decimal AvailableStock { get; set; }
        public decimal AvailableBaseStock { get; set; }
        public string UnitName { get; set; } = string.Empty;
    }
}
