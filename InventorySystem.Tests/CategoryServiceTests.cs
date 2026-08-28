using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using InventorySystem.Data;
using InventorySystem.DTOs.Categories;
using InventorySystem.DTOs.Products;
using InventorySystem.Models.Entities;
using InventorySystem.Repositories.Implementations;
using InventorySystem.Services.Implementations;
using Xunit;

namespace InventorySystem.Tests
{
    public class CategoryServiceTests
    {
        private ApplicationDbContext CreateContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            return new ApplicationDbContext(options);
        }

        private async Task<(CategoryService Service, ApplicationDbContext Context, Company CompanyA, Company CompanyB)> SetupAsync(string dbName)
        {
            var context = CreateContext(dbName);
            var companyA = new Company { CompanyID = 1, CompanyName = "Toyota", IsDeleted = false, CreatedAt = DateTime.UtcNow };
            var companyB = new Company { CompanyID = 2, CompanyName = "Nissan", IsDeleted = false, CreatedAt = DateTime.UtcNow };
            context.Companies.AddRange(companyA, companyB);
            await context.SaveChangesAsync();

            var repo = new CategoryRepository(context);
            var service = new CategoryService(repo, NullLogger<CategoryService>.Instance);
            return (service, context, companyA, companyB);
        }

        [Fact]
        public async Task CreateCategory_Succeeds()
        {
            var (service, context, companyA, _) = await SetupAsync(nameof(CreateCategory_Succeeds));

            var result = await service.CreateCategoryAsync(new CreateCategoryDto
            {
                CompanyID = companyA.CompanyID,
                Name = "SUV",
                IsActive = true
            }, userId: 1);

            Assert.True(result.Success);
            Assert.True(result.Data > 0);
            Assert.Equal(1, await context.Categories.CountAsync());
        }

        [Fact]
        public async Task CreateCategory_DuplicateNameSameCompany_Fails()
        {
            var (service, _, companyA, _) = await SetupAsync(nameof(CreateCategory_DuplicateNameSameCompany_Fails));

            await service.CreateCategoryAsync(new CreateCategoryDto
            {
                CompanyID = companyA.CompanyID,
                Name = "SUV",
                IsActive = true
            }, 1);

            var result = await service.CreateCategoryAsync(new CreateCategoryDto
            {
                CompanyID = companyA.CompanyID,
                Name = "SUV",
                IsActive = true
            }, 1);

            Assert.False(result.Success);
            Assert.Contains(result.Errors, e => e.Contains("already exists", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task CreateCategory_SameNameDifferentCompanies_Succeeds()
        {
            var (service, context, companyA, companyB) = await SetupAsync(nameof(CreateCategory_SameNameDifferentCompanies_Succeeds));

            var r1 = await service.CreateCategoryAsync(new CreateCategoryDto
            {
                CompanyID = companyA.CompanyID,
                Name = "SUV",
                IsActive = true
            }, 1);

            var r2 = await service.CreateCategoryAsync(new CreateCategoryDto
            {
                CompanyID = companyB.CompanyID,
                Name = "SUV",
                IsActive = true
            }, 1);

            Assert.True(r1.Success);
            Assert.True(r2.Success);
            Assert.Equal(2, await context.Categories.CountAsync());
        }

        [Fact]
        public async Task SoftDelete_UnusedCategory_Succeeds()
        {
            var (service, context, companyA, _) = await SetupAsync(nameof(SoftDelete_UnusedCategory_Succeeds));
            var create = await service.CreateCategoryAsync(new CreateCategoryDto
            {
                CompanyID = companyA.CompanyID,
                Name = "Sedan",
                IsActive = true
            }, 1);

            var result = await service.SoftDeleteCategoryAsync(create.Data, 1);

            Assert.True(result.Success);
            Assert.Equal(0, await context.Categories.CountAsync());
            Assert.Equal(1, await context.Categories.IgnoreQueryFilters().CountAsync(c => c.IsDeleted));
        }

        [Fact]
        public async Task SoftDelete_CategoryWithProducts_Fails()
        {
            var (service, context, companyA, _) = await SetupAsync(nameof(SoftDelete_CategoryWithProducts_Fails));
            var create = await service.CreateCategoryAsync(new CreateCategoryDto
            {
                CompanyID = companyA.CompanyID,
                Name = "Hatchback",
                IsActive = true
            }, 1);

            context.Units.Add(new Unit { UnitID = 1, UnitName = "PCS", IsActive = true });
            context.Products.Add(new Product
            {
                ProductID = 1,
                ProductName = "Corolla",
                CategoryID = create.Data,
                CompanyID = companyA.CompanyID,
                BaseUnitID = 1,
                BaseSellingPrice = 10m,
                IsActive = true
            });
            await context.SaveChangesAsync();

            var result = await service.SoftDeleteCategoryAsync(create.Data, 1);

            Assert.False(result.Success);
            Assert.Contains(result.Errors, e => e.Contains("assigned to one or more products", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task SoftDeletedCategory_DoesNotAppearInLookup()
        {
            var (service, context, companyA, _) = await SetupAsync(nameof(SoftDeletedCategory_DoesNotAppearInLookup));
            var create = await service.CreateCategoryAsync(new CreateCategoryDto
            {
                CompanyID = companyA.CompanyID,
                Name = "Pickup",
                IsActive = true
            }, 1);
            await service.SoftDeleteCategoryAsync(create.Data, 1);

            var lookup = new LookupService(context, new FakeUserCompanyAccessService(userId: 1));
            var items = await lookup.GetCategoriesAsync(companyA.CompanyID);

            Assert.DoesNotContain(items, i => i.Id == create.Data);
        }

        [Fact]
        public async Task UpdateCategory_CannotChangeCompany_WhenProductsExist()
        {
            var (service, context, companyA, companyB) = await SetupAsync(nameof(UpdateCategory_CannotChangeCompany_WhenProductsExist));
            var create = await service.CreateCategoryAsync(new CreateCategoryDto
            {
                CompanyID = companyA.CompanyID,
                Name = "Truck",
                IsActive = true
            }, 1);

            context.Units.Add(new Unit { UnitID = 1, UnitName = "PCS", IsActive = true });
            context.Products.Add(new Product
            {
                ProductID = 1,
                ProductName = "Hilux",
                CategoryID = create.Data,
                CompanyID = companyA.CompanyID,
                BaseUnitID = 1,
                BaseSellingPrice = 10m,
                IsActive = true
            });
            await context.SaveChangesAsync();

            var result = await service.UpdateCategoryAsync(new EditCategoryDto
            {
                CategoryID = create.Data,
                CompanyID = companyB.CompanyID,
                Name = "Truck",
                IsActive = true
            }, 1);

            Assert.False(result.Success);
            Assert.Contains(result.Errors, e => e.Contains("Cannot change the company", StringComparison.OrdinalIgnoreCase));
        }
    }

    public class ProductCategoryValidationTests
    {
        private ApplicationDbContext CreateContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            return new ApplicationDbContext(options);
        }

        private async Task<(ProductService Service, ApplicationDbContext Context, Company CoA, Company CoB, Category CatA, Category CatB, Unit Unit)> SetupAsync(string dbName)
        {
            var context = CreateContext(dbName);
            var coA = new Company { CompanyID = 1, CompanyName = "Toyota", IsDeleted = false };
            var coB = new Company { CompanyID = 2, CompanyName = "Nissan", IsDeleted = false };
            var catA = new Category { CategoryID = 1, CompanyID = 1, Name = "SUV", IsActive = true };
            var catB = new Category { CategoryID = 2, CompanyID = 2, Name = "SUV", IsActive = true };
            var unit = new Unit { UnitID = 1, UnitName = "PCS", IsActive = true };

            context.Companies.AddRange(coA, coB);
            context.Categories.AddRange(catA, catB);
            context.Units.Add(unit);
            await context.SaveChangesAsync();

            var repo = new ProductRepository(context);
            var service = new ProductService(repo, context, NullLogger<ProductService>.Instance);
            return (service, context, coA, coB, catA, catB, unit);
        }

        private static CreateProductDto ValidCreateDto(int companyId, int categoryId, int unitId, string sku = "SKU-1")
        {
            return new CreateProductDto
            {
                ProductName = "Test Product",
                CompanyID = companyId,
                CategoryID = categoryId,
                BaseUnitID = unitId,
                BaseSellingPrice = 10m,
                ReorderLevel = 0,
                IsActive = true,
                SKU = sku,
                Units = new List<ProductUnitDto>
                {
                    new ProductUnitDto
                    {
                        UnitID = unitId,
                        ConversionToBaseUnit = 1m,
                        SellingPrice = 10m,
                        IsDefaultPurchaseUnit = true,
                        IsDefaultSalesUnit = true
                    }
                }
            };
        }

        [Fact]
        public async Task CreateProduct_CategoryFromOtherCompany_Fails()
        {
            var (service, _, coA, _, _, catB, unit) = await SetupAsync(nameof(CreateProduct_CategoryFromOtherCompany_Fails));

            var result = await service.CreateProductAsync(ValidCreateDto(coA.CompanyID, catB.CategoryID, unit.UnitID), 1);

            Assert.False(result.Success);
            Assert.Contains(result.Errors, e => e.Contains("does not belong to the selected company", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task CreateProduct_MatchingCompanyAndCategory_Succeeds()
        {
            var (service, context, coA, _, catA, _, unit) = await SetupAsync(nameof(CreateProduct_MatchingCompanyAndCategory_Succeeds));

            var result = await service.CreateProductAsync(ValidCreateDto(coA.CompanyID, catA.CategoryID, unit.UnitID, "SKU-OK"), 1);

            Assert.True(result.Success);
            Assert.Equal(1, await context.Products.CountAsync());
        }

        [Fact]
        public async Task EditProduct_CategoryFromOtherCompany_Fails()
        {
            var (service, context, coA, _, catA, catB, unit) = await SetupAsync(nameof(EditProduct_CategoryFromOtherCompany_Fails));

            var create = await service.CreateProductAsync(ValidCreateDto(coA.CompanyID, catA.CategoryID, unit.UnitID, "SKU-EDIT"), 1);
            Assert.True(create.Success);

            var editDto = new EditProductDto
            {
                ProductID = create.Data,
                ProductName = "Test Product",
                CompanyID = coA.CompanyID,
                CategoryID = catB.CategoryID,
                BaseUnitID = unit.UnitID,
                BaseSellingPrice = 10m,
                ReorderLevel = 0,
                IsActive = true,
                SKU = "SKU-EDIT",
                Units = new List<ProductUnitDto>
                {
                    new ProductUnitDto
                    {
                        UnitID = unit.UnitID,
                        ConversionToBaseUnit = 1m,
                        SellingPrice = 10m,
                        IsDefaultPurchaseUnit = true,
                        IsDefaultSalesUnit = true
                    }
                }
            };

            var result = await service.UpdateProductAsync(editDto, 1);

            Assert.False(result.Success);
            Assert.Contains(result.Errors, e => e.Contains("does not belong to the selected company", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task ProductCompanyCategory_Integrity_HoldsAfterCreate()
        {
            var (service, context, coA, _, catA, _, unit) = await SetupAsync(nameof(ProductCompanyCategory_Integrity_HoldsAfterCreate));
            var result = await service.CreateProductAsync(ValidCreateDto(coA.CompanyID, catA.CategoryID, unit.UnitID, "SKU-INT"), 1);
            Assert.True(result.Success);

            var mismatched = await context.Products
                .Join(context.Categories, p => p.CategoryID, c => c.CategoryID, (p, c) => new { p, c })
                .AnyAsync(x => x.p.CompanyID != x.c.CompanyID);

            Assert.False(mismatched);
        }
    }
}
