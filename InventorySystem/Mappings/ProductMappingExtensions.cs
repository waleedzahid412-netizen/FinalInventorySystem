using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc.Rendering;
using InventorySystem.DTOs.Common;
using InventorySystem.DTOs.Products;
using InventorySystem.ViewModels.Products;

namespace InventorySystem.Mappings
{
    public static class ProductMappingExtensions
    {
        public static CreateProductDto ToDto(this CreateProductViewModel model)
        {
            return new CreateProductDto
            {
                ProductName = model.ProductName,
                Description = model.Description,
                SKU = model.SKU,
                Barcode = model.Barcode,
                CategoryID = model.CategoryID,
                CompanyID = model.CompanyID,
                BaseUnitID = model.BaseUnitID,
                BaseSellingPrice = model.BaseSellingPrice,
                AveragePurchaseCost = model.AveragePurchaseCost,
                ReorderLevel = model.ReorderLevel,
                IsActive = model.IsActive,
                Units = model.Units.Select(u => u.ToDto()).ToList()
            };
        }

        public static EditProductDto ToDto(this EditProductViewModel model)
        {
            return new EditProductDto
            {
                ProductID = model.ProductID,
                ProductName = model.ProductName,
                Description = model.Description,
                SKU = model.SKU,
                Barcode = model.Barcode,
                CategoryID = model.CategoryID,
                CompanyID = model.CompanyID,
                BaseUnitID = model.BaseUnitID,
                BaseSellingPrice = model.BaseSellingPrice,
                AveragePurchaseCost = model.AveragePurchaseCost,
                ReorderLevel = model.ReorderLevel,
                IsActive = model.IsActive,
                Units = model.Units.Select(u => u.ToDto()).ToList()
            };
        }

        public static ProductUnitDto ToDto(this ProductUnitInputViewModel model)
        {
            return new ProductUnitDto
            {
                ProductUnitID = model.ProductUnitID,
                UnitID = model.UnitID,
                UnitName = model.UnitName,
                ConversionToBaseUnit = model.ConversionToBaseUnit,
                PurchasePrice = model.PurchasePrice,
                SellingPrice = model.SellingPrice,
                IsDefaultPurchaseUnit = model.IsDefaultPurchaseUnit,
                IsDefaultSalesUnit = model.IsDefaultSalesUnit,
                IsActive = model.IsActive
            };
        }

        public static EditProductViewModel ToViewModel(this EditProductDto dto, IEnumerable<LookupItemDto> categories, IEnumerable<LookupItemDto> companies, IEnumerable<LookupItemDto> units)
        {
            return new EditProductViewModel
            {
                ProductID = dto.ProductID,
                ProductName = dto.ProductName,
                Description = dto.Description,
                SKU = dto.SKU,
                Barcode = dto.Barcode,
                CategoryID = dto.CategoryID,
                CompanyID = dto.CompanyID,
                BaseUnitID = dto.BaseUnitID,
                BaseSellingPrice = dto.BaseSellingPrice,
                AveragePurchaseCost = dto.AveragePurchaseCost,
                ReorderLevel = dto.ReorderLevel,
                IsActive = dto.IsActive,
                Units = dto.Units.Select(u => u.ToViewModel()).ToList(),
                Categories = categories.ToSelectList(dto.CategoryID),
                Companies = companies.ToSelectList(dto.CompanyID),
                AvailableUnits = units.ToSelectList(dto.BaseUnitID)
            };
        }

        public static ProductUnitInputViewModel ToViewModel(this ProductUnitDto dto)
        {
            return new ProductUnitInputViewModel
            {
                ProductUnitID = dto.ProductUnitID,
                UnitID = dto.UnitID,
                UnitName = dto.UnitName,
                ConversionToBaseUnit = dto.ConversionToBaseUnit,
                PurchasePrice = dto.PurchasePrice,
                SellingPrice = dto.SellingPrice,
                IsDefaultPurchaseUnit = dto.IsDefaultPurchaseUnit,
                IsDefaultSalesUnit = dto.IsDefaultSalesUnit,
                IsActive = dto.IsActive
            };
        }

        public static ProductDetailsViewModel ToViewModel(this ProductDetailsDto dto)
        {
            return new ProductDetailsViewModel
            {
                Product = dto
            };
        }

        public static List<SelectListItem> ToSelectList(this IEnumerable<LookupItemDto> items, int? selectedId = null)
        {
            return items.Select(i => new SelectListItem
            {
                Value = i.Id.ToString(),
                Text = i.Name,
                Selected = selectedId.HasValue && i.Id == selectedId.Value
            }).ToList();
        }
    }
}
