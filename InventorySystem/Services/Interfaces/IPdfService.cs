using System;
using InventorySystem.DTOs.Purchases;
using InventorySystem.DTOs.Sales;

namespace InventorySystem.Services.Interfaces
{
    public interface IPdfService
    {
        byte[] GenerateSalesInvoicePdf(SalesDetailsDto invoice);
        byte[] GeneratePurchaseInvoicePdf(PurchaseDetailsDto invoice);
        byte[] GenerateSalesReturnPdf(InventorySystem.DTOs.Returns.SalesReturnDetailsDto returnDto);
    }
}
