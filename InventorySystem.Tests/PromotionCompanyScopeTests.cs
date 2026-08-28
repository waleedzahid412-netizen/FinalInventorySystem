using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using InventorySystem.Data;
using InventorySystem.DTOs.Sales;
using InventorySystem.Models.Entities;
using InventorySystem.Services.Implementations;
using InventorySystem.ViewModels.Promotions;
using Xunit;

namespace InventorySystem.Tests
{
    public class PromotionCompanyScopeTests
    {
        private static ApplicationDbContext CreateDb(string name)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: name)
                .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            return new ApplicationDbContext(options);
        }

        private static async Task SeedCatalogAsync(ApplicationDbContext db)
        {
            db.Companies.AddRange(
                new Company { CompanyID = 1, CompanyName = "Company A" },
                new Company { CompanyID = 2, CompanyName = "Company B" });
            db.Categories.Add(new Category { CategoryID = 1, CompanyID = 1, Name = "Cat" });
            db.Units.Add(new Unit { UnitID = 1, UnitName = "PCS" });
            db.Roles.Add(new Role { RoleID = 1, RoleName = "Admin", IsActive = true });
            db.Users.Add(new User { UserID = 1, RoleID = 1, FullName = "Admin", Username = "a", PasswordHash = "x", IsActive = true });

            db.Products.AddRange(
                new Product
                {
                    ProductID = 1,
                    ProductName = "A Buy",
                    SKU = "A-BUY",
                    CompanyID = 1,
                    CategoryID = 1,
                    BaseUnitID = 1,
                    BaseSellingPrice = 10m,
                    IsActive = true
                },
                new Product
                {
                    ProductID = 2,
                    ProductName = "A Free",
                    SKU = "A-FREE",
                    CompanyID = 1,
                    CategoryID = 1,
                    BaseUnitID = 1,
                    BaseSellingPrice = 5m,
                    IsActive = true
                },
                new Product
                {
                    ProductID = 3,
                    ProductName = "B Free",
                    SKU = "B-FREE",
                    CompanyID = 2,
                    CategoryID = 1,
                    BaseUnitID = 1,
                    BaseSellingPrice = 5m,
                    IsActive = true
                });

            db.ProductUnits.AddRange(
                new ProductUnit { ProductUnitID = 1, ProductID = 1, UnitID = 1, ConversionToBaseUnit = 1, SellingPrice = 10m, IsDefaultSalesUnit = true, IsActive = true },
                new ProductUnit { ProductUnitID = 2, ProductID = 2, UnitID = 1, ConversionToBaseUnit = 1, SellingPrice = 5m, IsDefaultSalesUnit = true, IsActive = true },
                new ProductUnit { ProductUnitID = 3, ProductID = 3, UnitID = 1, ConversionToBaseUnit = 1, SellingPrice = 5m, IsDefaultSalesUnit = true, IsActive = true });

            await db.SaveChangesAsync();
        }

        [Fact]
        public async Task RuleValidate_RejectsCrossCompanyFreeProduct()
        {
            await using var db = CreateDb(nameof(RuleValidate_RejectsCrossCompanyFreeProduct));
            await SeedCatalogAsync(db);

            var rule = new PromotionRuleInputViewModel
            {
                BuyProductID = 1,
                BuyQuantity = 5,
                FreeRewardSelection = "3",
                FreeQuantity = 1
            };
            rule.NormalizeFreeReward();

            var provider = new SimpleServiceProvider(db);
            var results = rule.Validate(new ValidationContext(rule, provider, null)).ToList();

            Assert.Contains(results, r => r.ErrorMessage != null && r.ErrorMessage.Contains("BR-047", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task RuleValidate_AllowsCustomOtherFreeItem()
        {
            await using var db = CreateDb(nameof(RuleValidate_AllowsCustomOtherFreeItem));
            await SeedCatalogAsync(db);

            var rule = new PromotionRuleInputViewModel
            {
                BuyProductID = 1,
                BuyQuantity = 5,
                FreeRewardSelection = PromotionRuleInputViewModel.OtherRewardValue,
                CustomFreeItemName = "Free Mug",
                FreeQuantity = 1
            };
            rule.NormalizeFreeReward();

            var results = rule.Validate(new ValidationContext(rule, new SimpleServiceProvider(db), null)).ToList();
            Assert.Empty(results);
        }

        [Fact]
        public async Task Evaluate_SameCompanyPromo_Applies()
        {
            await using var db = CreateDb(nameof(Evaluate_SameCompanyPromo_Applies));
            await SeedCatalogAsync(db);

            db.PromotionCampaigns.Add(new PromotionCampaign
            {
                PromotionID = 1,
                Name = "A Promo",
                CompanyID = 1,
                IsActive = true,
                StartDate = DateTime.UtcNow.AddDays(-1),
                EndDate = DateTime.UtcNow.AddDays(30),
                CreatedBy = 1,
                PromotionRules =
                {
                    new PromotionRule
                    {
                        RuleID = 1,
                        PromotionID = 1,
                        BuyProductID = 1,
                        BuyQuantity = 5,
                        FreeProductID = 2,
                        FreeQuantity = 1
                    }
                }
            });
            await db.SaveChangesAsync();

            var service = new PromotionDiscountService(db);
            var result = await service.EvaluatePromotionsAndDiscountsAsync(new OrderContextDto
            {
                CompanyID = 1,
                SubTotal = 50m,
                Items =
                {
                    new CartItemDto { ProductID = 1, ProductUnitID = 1, Quantity = 5, UnitPrice = 10m, ItemType = "NORMAL" }
                }
            });

            Assert.Single(result.Promotions);
            Assert.Equal(1, result.Promotions[0].PromotionID);
            Assert.Equal(1, result.Promotions[0].RewardQuantity);
        }

        [Fact]
        public async Task Evaluate_CampaignForCompanyA_DoesNotFireOnCompanyBInvoice()
        {
            await using var db = CreateDb(nameof(Evaluate_CampaignForCompanyA_DoesNotFireOnCompanyBInvoice));
            await SeedCatalogAsync(db);

            db.PromotionCampaigns.Add(new PromotionCampaign
            {
                PromotionID = 1,
                Name = "A Only",
                CompanyID = 1,
                IsActive = true,
                StartDate = DateTime.UtcNow.AddDays(-1),
                EndDate = DateTime.UtcNow.AddDays(30),
                CreatedBy = 1,
                PromotionRules =
                {
                    new PromotionRule
                    {
                        RuleID = 1,
                        PromotionID = 1,
                        BuyProductID = 1,
                        BuyQuantity = 5,
                        FreeProductID = 2,
                        FreeQuantity = 1
                    }
                }
            });
            await db.SaveChangesAsync();

            var service = new PromotionDiscountService(db);
            var result = await service.EvaluatePromotionsAndDiscountsAsync(new OrderContextDto
            {
                CompanyID = 2,
                SubTotal = 50m,
                Items =
                {
                    new CartItemDto { ProductID = 1, ProductUnitID = 1, Quantity = 5, UnitPrice = 10m, ItemType = "NORMAL" }
                }
            });

            Assert.Empty(result.Promotions);
        }

        [Fact]
        public async Task Evaluate_SkipsCrossCompanyFreeProductRule()
        {
            await using var db = CreateDb(nameof(Evaluate_SkipsCrossCompanyFreeProductRule));
            await SeedCatalogAsync(db);

            db.PromotionCampaigns.Add(new PromotionCampaign
            {
                PromotionID = 1,
                Name = "Bad Cross",
                CompanyID = 1,
                IsActive = true,
                StartDate = DateTime.UtcNow.AddDays(-1),
                EndDate = DateTime.UtcNow.AddDays(30),
                CreatedBy = 1,
                PromotionRules =
                {
                    new PromotionRule
                    {
                        RuleID = 1,
                        PromotionID = 1,
                        BuyProductID = 1,
                        BuyQuantity = 5,
                        FreeProductID = 3, // company 2
                        FreeQuantity = 1
                    }
                }
            });
            await db.SaveChangesAsync();

            var service = new PromotionDiscountService(db);
            var result = await service.EvaluatePromotionsAndDiscountsAsync(new OrderContextDto
            {
                CompanyID = 1,
                SubTotal = 50m,
                Items =
                {
                    new CartItemDto { ProductID = 1, ProductUnitID = 1, Quantity = 5, UnitPrice = 10m, ItemType = "NORMAL" }
                }
            });

            Assert.Empty(result.Promotions);
        }

        [Fact]
        public async Task Evaluate_AllowsCustomOtherFreeItem()
        {
            await using var db = CreateDb(nameof(Evaluate_AllowsCustomOtherFreeItem));
            await SeedCatalogAsync(db);

            db.PromotionCampaigns.Add(new PromotionCampaign
            {
                PromotionID = 1,
                Name = "Custom Other",
                CompanyID = 1,
                IsActive = true,
                StartDate = DateTime.UtcNow.AddDays(-1),
                EndDate = DateTime.UtcNow.AddDays(30),
                CreatedBy = 1,
                PromotionRules =
                {
                    new PromotionRule
                    {
                        RuleID = 1,
                        PromotionID = 1,
                        BuyProductID = 1,
                        BuyQuantity = 5,
                        FreeProductID = null,
                        IsCustomFreeItem = true,
                        CustomFreeItemName = "Mug",
                        FreeQuantity = 1
                    }
                }
            });
            await db.SaveChangesAsync();

            var service = new PromotionDiscountService(db);
            var result = await service.EvaluatePromotionsAndDiscountsAsync(new OrderContextDto
            {
                CompanyID = 1,
                SubTotal = 50m,
                Items =
                {
                    new CartItemDto { ProductID = 1, ProductUnitID = 1, Quantity = 5, UnitPrice = 10m, ItemType = "NORMAL" }
                }
            });

            Assert.Single(result.Promotions);
            Assert.True(result.Promotions[0].IsCustomFreeItem);
            Assert.Equal("Mug", result.Promotions[0].CustomFreeItemName);
        }

        private sealed class SimpleServiceProvider : IServiceProvider
        {
            private readonly ApplicationDbContext _db;

            public SimpleServiceProvider(ApplicationDbContext db) => _db = db;

            public object? GetService(Type serviceType) =>
                serviceType == typeof(ApplicationDbContext) ? _db : null;
        }
    }
}
