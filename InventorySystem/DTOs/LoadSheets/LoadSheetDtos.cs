using System;
using System.Collections.Generic;

namespace InventorySystem.DTOs.LoadSheets
{
    public class LoadSheetFilterDto
    {
        public int BookerID { get; set; }
        public DateTime Date { get; set; } = DateTime.Today;
        public int? SupplierID { get; set; }
    }

    public class LoadSheetProductRowDto
    {
        public int ProductID { get; set; }
        public string ProductIdDisplay { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        /// <summary>True when this row is aggregated from FREE invoice lines (kept separate from paid rows).</summary>
        public bool IsFree { get; set; }
        /// <summary>Selling price per smallest (base) unit, derived from historical line UnitPrice / ConversionToBaseUnit.</summary>
        public decimal UnitPrice { get; set; }
        /// <summary>Net quantity to load in base units (ConvertedQuantity minus returns).</summary>
        public decimal TotalQuantity { get; set; }
        public string BaseUnitName { get; set; } = string.Empty;
    }

    public class LoadSheetInvoiceRowDto
    {
        public int InvoiceID { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; }
        public string PartyName { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public decimal Discount { get; set; }
        public decimal TotalAmount { get; set; }
    }

    public class LoadSheetDto
    {
        public string BookerName { get; set; } = string.Empty;
        public DateTime FilterDate { get; set; }
        public string SupplierDisplay { get; set; } = "All Suppliers";
        public string SalespersonDisplay { get; set; } = string.Empty;
        public bool HasInvoices { get; set; }
        public string? EmptyMessage { get; set; }
        public List<LoadSheetProductRowDto> ProductRows { get; set; } = new();
        public List<LoadSheetInvoiceRowDto> InvoiceRows { get; set; } = new();
        public decimal TotalAmountSum { get; set; }
        public decimal TotalDiscountSum { get; set; }
        public decimal TotalGrandSum { get; set; }
        public decimal TotalCountSum { get; set; }
    }
}
