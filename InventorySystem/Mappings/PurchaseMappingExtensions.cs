using System;
using System.Collections.Generic;
using System.Text.Json;
using InventorySystem.DTOs.Purchases;
using InventorySystem.ViewModels.Purchases;

namespace InventorySystem.Mappings
{
    public static class PurchaseMappingExtensions
    {
        public static CreatePurchaseInvoiceDto ToDto(this CreatePurchaseViewModel vm)
        {
            var dto = new CreatePurchaseInvoiceDto
            {
                CompanyID = vm.CompanyID,
                WarehouseID = vm.WarehouseID,
                SupplierInvoiceNumber = vm.SupplierInvoiceNumber,
                InvoiceDate = vm.InvoiceDate,
                Notes = vm.Notes,
                Items = new List<CreatePurchaseItemDto>()
            };

            if (!string.IsNullOrWhiteSpace(vm.ItemsJson))
            {
                try
                {
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };

                    var items = JsonSerializer.Deserialize<List<CreatePurchaseItemDto>>(vm.ItemsJson, options);
                    if (items != null)
                    {
                        dto.Items = items;
                    }
                }
                catch
                {
                    // Fallback to empty list; validation in service will report missing items
                }
            }

            return dto;
        }

        public static PurchaseDetailsViewModel ToViewModel(this PurchaseDetailsDto dto)
        {
            return new PurchaseDetailsViewModel
            {
                Header = dto.Header,
                Items = dto.Items,
                Financial = dto.Financial
            };
        }
    }
}
