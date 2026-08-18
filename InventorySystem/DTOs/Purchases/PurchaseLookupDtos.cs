namespace InventorySystem.DTOs.Purchases
{
    public class WarehouseLookupDto
    {
        public int WarehouseID { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class ProductUnitLookupDto
    {
        public int ProductUnitID { get; set; }
        public int UnitID { get; set; }
        public string UnitName { get; set; } = string.Empty;
        public decimal ConversionToBaseUnit { get; set; }
        public decimal? PurchasePrice { get; set; }
        public bool IsDefaultPurchaseUnit { get; set; }
        public bool IsBaseUnit => ConversionToBaseUnit == 1m;
    }

    public class ProductInfoDto
    {
        public int ProductID { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string SKU { get; set; } = string.Empty;
        public int BaseUnitID { get; set; }
        public string BaseUnitName { get; set; } = string.Empty;
    }
}
