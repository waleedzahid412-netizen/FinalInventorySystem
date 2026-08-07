using System;
using System.Collections.Generic;
using System.Text.Json;
using InventorySystem.DTOs.Sales;
using InventorySystem.ViewModels.Sales;

namespace InventorySystem.Mappings
{
    public static class SalesMappingExtensions
    {
        public static CreateSalesInvoiceDto ToDto(this CreateSalesViewModel model)
        {
            var dto = new CreateSalesInvoiceDto
            {
                CustomerID = model.CustomerID,
                WarehouseID = model.WarehouseID,
                DeliveryPersonID = model.DeliveryPersonID,
                InvoiceNumber = model.InvoiceNumber,
                InvoiceDate = model.InvoiceDate,
                Remarks = model.Remarks,
                AppliedDiscountRuleID = model.AppliedDiscountRuleID,
                Items = new List<CreateSalesItemDto>()
            };

            if (!string.IsNullOrWhiteSpace(model.ItemsJson))
            {
                try
                {
                    var parsedItems = JsonSerializer.Deserialize<List<CreateSalesItemInputViewModel>>(
                        model.ItemsJson,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (parsedItems != null)
                    {
                        foreach (var item in parsedItems)
                        {
                            dto.Items.Add(new CreateSalesItemDto
                            {
                                ProductID = item.ProductID,
                                ProductUnitID = item.ProductUnitID,
                                Quantity = item.Quantity,
                                UnitPrice = item.UnitPrice,
                                DiscountAmount = item.DiscountAmount,
                                ItemType = string.IsNullOrWhiteSpace(item.ItemType) ? "NORMAL" : item.ItemType,
                                PromotionID = item.PromotionID
                            });
                        }
                    }
                }
                catch
                {
                    // Fallback to empty items; validation will trigger gracefully
                }
            }

            return dto;
        }

        public static SalesDetailsViewModel ToViewModel(this SalesDetailsDto dto)
        {
            return new SalesDetailsViewModel
            {
                Details = dto
            };
        }
    }
}
