namespace InventorySystem.DTOs.Sales
{
    public class CustomerInfoDto
    {
        public int CustomerID { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string? ShopName { get; set; }
        public decimal CreditLimit { get; set; }
        public decimal CurrentOutstanding { get; set; }
        public int? AreaID { get; set; }
        public string? AreaName { get; set; }
        public int? SubAreaID { get; set; }
        public string? SubAreaName { get; set; }
        /// <summary>Optional preferred invoice discount % from customer master. Null = unset.</summary>
        public decimal? PreferredDiscountPercent { get; set; }
        public bool ExceedsCreditLimit => CreditLimit > 0 && CurrentOutstanding > CreditLimit;
    }
}
