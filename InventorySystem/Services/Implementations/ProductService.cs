using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using InventorySystem.Data;
using InventorySystem.DTOs.Common;
using InventorySystem.DTOs.Products;
using InventorySystem.Models.Entities;
using InventorySystem.Repositories.Interfaces;
using InventorySystem.Services.Interfaces;

namespace InventorySystem.Services.Implementations
{
    public class ProductService : IProductService
    {
        private readonly IProductRepository _productRepository;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ProductService> _logger;

        public ProductService(
            IProductRepository productRepository,
            ApplicationDbContext context,
            ILogger<ProductService> logger)
        {
            _productRepository = productRepository;
            _context = context;
            _logger = logger;
        }

        public async Task<PagedResult<ProductListItemDto>> GetPagedProductsAsync(ProductFilterDto filter, CancellationToken cancellationToken = default)
        {
            var pagedProducts = await _productRepository.GetPagedAsync(filter, cancellationToken);

            var listItems = pagedProducts.Items.Select(p => new ProductListItemDto
            {
                ProductID = p.ProductID,
                ProductName = p.ProductName,
                Description = p.Description,
                SKU = p.SKU,
                Barcode = p.Barcode,
                CategoryID = p.CategoryID,
                CategoryName = p.Category?.Name ?? string.Empty,
                CompanyID = p.CompanyID,
                CompanyName = p.Company?.CompanyName ?? string.Empty,
                BaseUnitName = p.BaseUnit?.UnitName ?? string.Empty,
                BaseSellingPrice = p.BaseSellingPrice,
                AveragePurchaseCost = p.AveragePurchaseCost,
                CurrentStock = p.InventoryStocks?.Sum(s => s.Quantity) ?? 0m,
                IsActive = p.IsActive,
                CreatedAt = p.CreatedAt
            }).ToList();

            return new PagedResult<ProductListItemDto>(listItems, pagedProducts.TotalCount, pagedProducts.PageNumber, pagedProducts.PageSize);
        }

        public async Task<ProductDetailsDto?> GetProductDetailsAsync(int productId, CancellationToken cancellationToken = default)
        {
            var product = await _productRepository.GetByIdWithUnitsAsync(productId, cancellationToken);
            if (product == null) return null;

            var inventorySummary = await _productRepository.GetInventorySummaryAsync(productId, cancellationToken);
            var warehouseStocks = await _productRepository.GetWarehouseStocksAsync(productId, cancellationToken);

            var productUnits = product.ProductUnits
                .Where(pu => !pu.IsDeleted)
                .Select(pu => new ProductUnitDto
                {
                    ProductUnitID = pu.ProductUnitID,
                    UnitID = pu.UnitID,
                    UnitName = pu.Unit?.UnitName ?? string.Empty,
                    ConversionToBaseUnit = pu.ConversionToBaseUnit,
                    PurchasePrice = pu.PurchasePrice,
                    SellingPrice = pu.SellingPrice,
                    IsDefaultPurchaseUnit = pu.IsDefaultPurchaseUnit,
                    IsDefaultSalesUnit = pu.IsDefaultSalesUnit,
                    IsActive = pu.IsActive
                }).ToList();

            return new ProductDetailsDto
            {
                ProductID = product.ProductID,
                ProductName = product.ProductName,
                Description = product.Description,
                SKU = product.SKU,
                Barcode = product.Barcode,
                CategoryID = product.CategoryID,
                CategoryName = product.Category?.Name ?? string.Empty,
                CompanyID = product.CompanyID,
                CompanyName = product.Company?.CompanyName ?? string.Empty,
                BaseUnitID = product.BaseUnitID,
                BaseUnitName = product.BaseUnit?.UnitName ?? string.Empty,
                BaseSellingPrice = product.BaseSellingPrice,
                AveragePurchaseCost = product.AveragePurchaseCost,
                ReorderLevel = product.ReorderLevel,
                IsActive = product.IsActive,
                CreatedAt = product.CreatedAt,
                InventorySummary = inventorySummary,
                WarehouseStocks = warehouseStocks,
                ProductUnits = productUnits
            };
        }

        public async Task<EditProductDto?> GetProductForEditAsync(int productId, CancellationToken cancellationToken = default)
        {
            var product = await _productRepository.GetByIdWithUnitsAsync(productId, cancellationToken);
            if (product == null) return null;

            return new EditProductDto
            {
                ProductID = product.ProductID,
                ProductName = product.ProductName,
                Description = product.Description,
                SKU = product.SKU,
                Barcode = product.Barcode,
                CategoryID = product.CategoryID,
                CompanyID = product.CompanyID,
                BaseUnitID = product.BaseUnitID,
                BaseSellingPrice = product.BaseSellingPrice,
                AveragePurchaseCost = product.AveragePurchaseCost,
                ReorderLevel = product.ReorderLevel,
                IsActive = product.IsActive,
                Units = product.ProductUnits
                    .Where(pu => !pu.IsDeleted)
                    .Select(pu => new ProductUnitDto
                    {
                        ProductUnitID = pu.ProductUnitID,
                        UnitID = pu.UnitID,
                        UnitName = pu.Unit?.UnitName ?? string.Empty,
                        ConversionToBaseUnit = pu.ConversionToBaseUnit,
                        PurchasePrice = pu.PurchasePrice,
                        SellingPrice = pu.SellingPrice,
                        IsDefaultPurchaseUnit = pu.IsDefaultPurchaseUnit,
                        IsDefaultSalesUnit = pu.IsDefaultSalesUnit,
                        IsActive = pu.IsActive
                    }).ToList()
            };
        }

        public async Task<OperationResult<int>> CreateProductAsync(CreateProductDto dto, int userId, CancellationToken cancellationToken = default)
        {
            var validationErrors = await ValidateCreateProductDtoAsync(dto, cancellationToken);
            if (validationErrors.Any())
            {
                return OperationResult<int>.Fail(validationErrors);
            }

            using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var now = DateTime.UtcNow;
                var product = new Product
                {
                    ProductName = dto.ProductName.Trim(),
                    Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
                    SKU = string.IsNullOrWhiteSpace(dto.SKU) ? null : dto.SKU.Trim(),
                    Barcode = string.IsNullOrWhiteSpace(dto.Barcode) ? null : dto.Barcode.Trim(),
                    CategoryID = dto.CategoryID,
                    CompanyID = dto.CompanyID,
                    BaseUnitID = dto.BaseUnitID,
                    BaseSellingPrice = dto.BaseSellingPrice,
                    AveragePurchaseCost = dto.AveragePurchaseCost,
                    ReorderLevel = dto.ReorderLevel,
                    IsActive = dto.IsActive,
                    CreatedAt = now,
                    CreatedBy = userId,
                    IsDeleted = false
                };

                await _productRepository.AddAsync(product, cancellationToken);
                await _productRepository.SaveChangesAsync(cancellationToken);

                // Add ProductUnits
                foreach (var unitDto in dto.Units)
                {
                    var productUnit = new ProductUnit
                    {
                        ProductID = product.ProductID,
                        UnitID = unitDto.UnitID,
                        ConversionToBaseUnit = unitDto.ConversionToBaseUnit,
                        PurchasePrice = unitDto.PurchasePrice,
                        SellingPrice = unitDto.SellingPrice,
                        IsDefaultPurchaseUnit = unitDto.IsDefaultPurchaseUnit,
                        IsDefaultSalesUnit = unitDto.IsDefaultSalesUnit,
                        IsActive = unitDto.IsActive,
                        CreatedAt = now,
                        CreatedBy = userId,
                        IsDeleted = false
                    };
                    await _context.ProductUnits.AddAsync(productUnit, cancellationToken);
                }

                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                _logger.LogInformation("Product created successfully with ID {ProductID}", product.ProductID);
                return OperationResult<int>.Ok(product.ProductID, "Product created successfully.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "Error occurred while creating product {ProductName}", dto.ProductName);
                return OperationResult<int>.Fail($"Failed to create product: {ex.Message}");
            }
        }

        public async Task<OperationResult> UpdateProductAsync(EditProductDto dto, int userId, CancellationToken cancellationToken = default)
        {
            var validationErrors = await ValidateEditProductDtoAsync(dto, cancellationToken);
            if (validationErrors.Any())
            {
                return OperationResult.Fail(validationErrors);
            }

            var product = await _productRepository.GetByIdWithUnitsAsync(dto.ProductID, cancellationToken);
            if (product == null)
            {
                return OperationResult.Fail("Product not found.");
            }

            using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var now = DateTime.UtcNow;
                product.ProductName = dto.ProductName.Trim();
                product.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
                product.SKU = string.IsNullOrWhiteSpace(dto.SKU) ? null : dto.SKU.Trim();
                product.Barcode = string.IsNullOrWhiteSpace(dto.Barcode) ? null : dto.Barcode.Trim();
                product.CategoryID = dto.CategoryID;
                product.CompanyID = dto.CompanyID;
                product.BaseSellingPrice = dto.BaseSellingPrice;
                product.AveragePurchaseCost = dto.AveragePurchaseCost;
                product.ReorderLevel = dto.ReorderLevel;
                product.IsActive = dto.IsActive;
                product.UpdatedAt = now;
                product.UpdatedBy = userId;

                // Sync ProductUnits
                var existingUnits = product.ProductUnits.Where(pu => !pu.IsDeleted).ToList();
                var incomingUnitIds = dto.Units.Select(u => u.UnitID).ToList();

                // Soft delete units removed in edit form
                foreach (var existing in existingUnits)
                {
                    if (!incomingUnitIds.Contains(existing.UnitID))
                    {
                        existing.IsDeleted = true;
                        existing.DeletedAt = now;
                        existing.UpdatedBy = userId;
                        existing.UpdatedAt = now;
                    }
                }

                // Add or update incoming units
                foreach (var unitDto in dto.Units)
                {
                    var existing = existingUnits.FirstOrDefault(u => u.UnitID == unitDto.UnitID);
                    if (existing != null)
                    {
                        existing.ConversionToBaseUnit = unitDto.ConversionToBaseUnit;
                        existing.PurchasePrice = unitDto.PurchasePrice;
                        existing.SellingPrice = unitDto.SellingPrice;
                        existing.IsDefaultPurchaseUnit = unitDto.IsDefaultPurchaseUnit;
                        existing.IsDefaultSalesUnit = unitDto.IsDefaultSalesUnit;
                        existing.IsActive = unitDto.IsActive;
                        existing.UpdatedAt = now;
                        existing.UpdatedBy = userId;
                    }
                    else
                    {
                        var newUnit = new ProductUnit
                        {
                            ProductID = product.ProductID,
                            UnitID = unitDto.UnitID,
                            ConversionToBaseUnit = unitDto.ConversionToBaseUnit,
                            PurchasePrice = unitDto.PurchasePrice,
                            SellingPrice = unitDto.SellingPrice,
                            IsDefaultPurchaseUnit = unitDto.IsDefaultPurchaseUnit,
                            IsDefaultSalesUnit = unitDto.IsDefaultSalesUnit,
                            IsActive = unitDto.IsActive,
                            CreatedAt = now,
                            CreatedBy = userId,
                            IsDeleted = false
                        };
                        await _context.ProductUnits.AddAsync(newUnit, cancellationToken);
                    }
                }

                await _productRepository.UpdateAsync(product, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                _logger.LogInformation("Product updated successfully for ID {ProductID}", product.ProductID);
                return OperationResult.Ok("Product updated successfully.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "Error occurred while updating product ID {ProductID}", dto.ProductID);
                return OperationResult.Fail($"Failed to update product: {ex.Message}");
            }
        }

        public async Task<OperationResult> SoftDeleteProductAsync(int productId, int userId, CancellationToken cancellationToken = default)
        {
            var product = await _productRepository.GetByIdWithUnitsAsync(productId, cancellationToken);
            if (product == null)
            {
                return OperationResult.Fail("Product not found.");
            }

            try
            {
                await _productRepository.SoftDeleteAsync(productId, userId, cancellationToken);
                await _productRepository.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Product soft-deleted successfully for ID {ProductID}", productId);
                return OperationResult.Ok("Product deleted successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while soft-deleting product ID {ProductID}", productId);
                return OperationResult.Fail($"Failed to delete product: {ex.Message}");
            }
        }

        public async Task<bool> ValidateSkuAsync(string sku, int? excludeProductId = null, CancellationToken cancellationToken = default)
        {
            return await _productRepository.IsSkuUniqueAsync(sku, excludeProductId, cancellationToken);
        }

        public async Task<bool> ValidateBarcodeAsync(string barcode, int? excludeProductId = null, CancellationToken cancellationToken = default)
        {
            return await _productRepository.IsBarcodeUniqueAsync(barcode, excludeProductId, cancellationToken);
        }

        public async Task<PagedResult<ProductPurchaseHistoryDto>> GetPurchaseHistoryAsync(int productId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        {
            return await _productRepository.GetPurchaseHistoryAsync(productId, pageNumber, pageSize, cancellationToken);
        }

        public async Task<PagedResult<ProductSalesHistoryDto>> GetSalesHistoryAsync(int productId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        {
            return await _productRepository.GetSalesHistoryAsync(productId, pageNumber, pageSize, cancellationToken);
        }

        public async Task<PagedResult<ProductInventoryTransactionDto>> GetInventoryTransactionsAsync(int productId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        {
            return await _productRepository.GetInventoryTransactionsAsync(productId, pageNumber, pageSize, cancellationToken);
        }

        private async Task<List<string>> ValidateCreateProductDtoAsync(CreateProductDto dto, CancellationToken cancellationToken)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(dto.ProductName))
            {
                errors.Add("Product Name is required.");
            }

            if (dto.CompanyID <= 0)
            {
                errors.Add("Company is required.");
            }
            else
            {
                bool companyExists = await _context.Companies.AnyAsync(c => c.CompanyID == dto.CompanyID, cancellationToken);
                if (!companyExists)
                {
                    errors.Add("Selected company does not exist.");
                }
            }

            if (dto.CategoryID <= 0)
            {
                errors.Add("Category is required.");
            }
            else
            {
                await ValidateProductCategoryCompanyAsync(dto.CategoryID, dto.CompanyID, errors, cancellationToken);
            }

            if (dto.BaseUnitID <= 0)
            {
                errors.Add("Base Unit is required.");
            }

            if (dto.BaseSellingPrice < 0)
            {
                errors.Add("Base Selling Price cannot be negative.");
            }

            if (dto.ReorderLevel < 0)
            {
                errors.Add("Reorder Level cannot be negative.");
            }

            if (!string.IsNullOrWhiteSpace(dto.SKU))
            {
                bool isSkuUnique = await _productRepository.IsSkuUniqueAsync(dto.SKU, null, cancellationToken);
                if (!isSkuUnique)
                {
                    errors.Add("SKU already exists.");
                }
            }

            if (!string.IsNullOrWhiteSpace(dto.Barcode))
            {
                bool isBarcodeUnique = await _productRepository.IsBarcodeUniqueAsync(dto.Barcode, null, cancellationToken);
                if (!isBarcodeUnique)
                {
                    errors.Add("Barcode already exists.");
                }
            }

            ValidateProductUnits(dto.BaseUnitID, dto.Units, errors);

            return errors;
        }

        private async Task<List<string>> ValidateEditProductDtoAsync(EditProductDto dto, CancellationToken cancellationToken)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(dto.ProductName))
            {
                errors.Add("Product Name is required.");
            }

            if (dto.CompanyID <= 0)
            {
                errors.Add("Company is required.");
            }
            else
            {
                bool companyExists = await _context.Companies.AnyAsync(c => c.CompanyID == dto.CompanyID, cancellationToken);
                if (!companyExists)
                {
                    errors.Add("Selected company does not exist.");
                }
            }

            if (dto.CategoryID <= 0)
            {
                errors.Add("Category is required.");
            }
            else
            {
                await ValidateProductCategoryCompanyAsync(dto.CategoryID, dto.CompanyID, errors, cancellationToken);
            }

            if (dto.BaseSellingPrice < 0)
            {
                errors.Add("Base Selling Price cannot be negative.");
            }

            if (dto.ReorderLevel < 0)
            {
                errors.Add("Reorder Level cannot be negative.");
            }

            if (!string.IsNullOrWhiteSpace(dto.SKU))
            {
                bool isSkuUnique = await _productRepository.IsSkuUniqueAsync(dto.SKU, dto.ProductID, cancellationToken);
                if (!isSkuUnique)
                {
                    errors.Add("SKU already exists.");
                }
            }

            if (!string.IsNullOrWhiteSpace(dto.Barcode))
            {
                bool isBarcodeUnique = await _productRepository.IsBarcodeUniqueAsync(dto.Barcode, dto.ProductID, cancellationToken);
                if (!isBarcodeUnique)
                {
                    errors.Add("Barcode already exists.");
                }
            }

            ValidateProductUnits(dto.BaseUnitID, dto.Units, errors);

            return errors;
        }

        private async Task ValidateProductCategoryCompanyAsync(int categoryId, int companyId, List<string> errors, CancellationToken cancellationToken)
        {
            var category = await _context.Categories
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.CategoryID == categoryId, cancellationToken);

            if (category == null)
            {
                errors.Add("Selected category does not exist.");
                return;
            }

            if (companyId > 0 && category.CompanyID != companyId)
            {
                errors.Add("The selected category does not belong to the selected company.");
            }
        }

        private static void ValidateProductUnits(int baseUnitId, List<ProductUnitDto> units, List<string> errors)
        {
            if (units == null || !units.Any())
            {
                errors.Add("At least one packaging/selling unit mapping is required.");
                return;
            }

            // Duplicate unit validation
            var duplicateUnitIds = units.GroupBy(u => u.UnitID).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (duplicateUnitIds.Any())
            {
                errors.Add("Duplicate packaging units are not allowed.");
            }

            // Conversion factor validation
            if (units.Any(u => u.ConversionToBaseUnit <= 0))
            {
                errors.Add("Unit conversion factors must be greater than zero.");
            }

            // Base Unit conversion validation
            var baseUnitMapping = units.FirstOrDefault(u => u.UnitID == baseUnitId);
            if (baseUnitMapping != null && baseUnitMapping.ConversionToBaseUnit != 1m)
            {
                errors.Add("Base Unit conversion factor must equal 1.");
            }

            // Default purchase unit validation
            int defaultPurchaseCount = units.Count(u => u.IsDefaultPurchaseUnit);
            if (defaultPurchaseCount != 1)
            {
                errors.Add("Exactly one Default Purchase Unit must be selected.");
            }

            // Default sales unit validation
            int defaultSalesCount = units.Count(u => u.IsDefaultSalesUnit);
            if (defaultSalesCount != 1)
            {
                errors.Add("Exactly one Default Sales Unit must be selected.");
            }
        }
    }
}
