using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using InventorySystem.Data;
using InventorySystem.DTOs.Sales;
using InventorySystem.Models.Entities;
using InventorySystem.Repositories.Implementations;
using InventorySystem.Services.Implementations;
using Xunit;

namespace InventorySystem.Tests
{
    public class InvoiceEditGuardTests
    {
        [Fact]
        public async Task CanEditSalesInvoiceAsync_WhenIsLocked_ReturnsFail()
        {
            await using var context = CreateContext(nameof(CanEditSalesInvoiceAsync_WhenIsLocked_ReturnsFail));
            var service = CreateSalesService(context);
            var invoiceId = await SeedLockedInvoiceAsync(context);

            var result = await service.CanEditSalesInvoiceAsync(invoiceId);

            Assert.False(result.Success);
            Assert.Contains("locked", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task UpdateSalesInvoiceAsync_WhenIsLocked_ReturnsFail()
        {
            await using var context = CreateContext(nameof(UpdateSalesInvoiceAsync_WhenIsLocked_ReturnsFail));
            var service = CreateSalesService(context);
            var invoiceId = await SeedLockedInvoiceAsync(context);

            var result = await service.UpdateSalesInvoiceAsync(new UpdateSalesInvoiceDto
            {
                InvoiceID = invoiceId,
                InvoiceDate = DateTime.Today,
                BookerID = 1,
                SalespersonID = 1,
                EditReason = "Attempted edit on locked invoice",
                ApplyPromotions = false,
                Items = new List<CreateSalesItemDto>
                {
                    new() { ProductID = 1, ProductUnitID = 1, Quantity = 1m, UnitPrice = 100m, ItemType = "NORMAL" }
                }
            }, userId: 1);

            Assert.False(result.Success);
            Assert.Contains("locked", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task CanEditSalesInvoiceAsync_WhenUnlocked_ReturnsOk()
        {
            await using var context = CreateContext(nameof(CanEditSalesInvoiceAsync_WhenUnlocked_ReturnsOk));
            var service = CreateSalesService(context);
            var invoiceId = await SeedUnlockedInvoiceAsync(context);

            var result = await service.CanEditSalesInvoiceAsync(invoiceId);

            Assert.True(result.Success);
        }

        private static SalesService CreateSalesService(ApplicationDbContext context) =>
            new SalesService(
                new SalesRepository(context),
                context,
                new PromotionDiscountService(context),
                new FakeCompanyContext(1, "Co"),
                TestFifoHelper.CreateFifo(context),
                NullLogger<SalesService>.Instance);

        private static async Task<int> SeedLockedInvoiceAsync(ApplicationDbContext context)
        {
            await SeedReferenceDataAsync(context);

            var invoice = new SalesInvoice
            {
                CustomerID = 1,
                CompanyID = 1,
                BookerID = 1,
                SalespersonID = 1,
                WarehouseID = 1,
                CreatedBy = 1,
                InvoiceNumber = "SINV-LOCKED-001",
                InvoiceDate = DateTime.UtcNow.Date,
                SubTotal = 1000m,
                GrandTotal = 1000m,
                PaidAmount = 0m,
                PaymentStatus = "UNPAID",
                IsLocked = true,
                DiscountMode = "None",
                IsDeleted = false
            };
            context.SalesInvoices.Add(invoice);
            await context.SaveChangesAsync();
            return invoice.InvoiceID;
        }

        private static async Task<int> SeedUnlockedInvoiceAsync(ApplicationDbContext context)
        {
            await SeedReferenceDataAsync(context);

            var invoice = new SalesInvoice
            {
                CustomerID = 1,
                CompanyID = 1,
                BookerID = 1,
                SalespersonID = 1,
                WarehouseID = 1,
                CreatedBy = 1,
                InvoiceNumber = "SINV-OPEN-001",
                InvoiceDate = DateTime.UtcNow.Date,
                SubTotal = 1000m,
                GrandTotal = 1000m,
                PaidAmount = 0m,
                PaymentStatus = "UNPAID",
                IsLocked = false,
                DiscountMode = "None",
                IsDeleted = false
            };
            context.SalesInvoices.Add(invoice);
            await context.SaveChangesAsync();
            return invoice.InvoiceID;
        }

        private static async Task SeedReferenceDataAsync(ApplicationDbContext context)
        {
            context.Companies.Add(new Company { CompanyID = 1, CompanyName = "Co" });
            context.Warehouses.Add(new Warehouse { WarehouseID = 1, Name = "Main", IsActive = true });
            context.Units.Add(new Unit { UnitID = 1, UnitName = "Piece" });
            context.Categories.Add(new Category { CategoryID = 1, CompanyID = 1, Name = "Cat" });
            context.Roles.Add(new Role { RoleID = 1, RoleName = "Admin", IsActive = true });
            context.Users.Add(new User { UserID = 1, RoleID = 1, FullName = "U", Username = "u", PasswordHash = "x", IsActive = true });
            context.Customers.Add(new Customer { CustomerID = 1, ShopName = "Shop", Address = "A", IsActive = true });
            context.Bookers.Add(new Booker { BookerID = 1, CompanyID = 1, Name = "B", CNIC = "35202-1111111-1", IsActive = true });
            context.Products.Add(new Product
            {
                ProductID = 1,
                ProductName = "P",
                SKU = "P1",
                CategoryID = 1,
                CompanyID = 1,
                BaseUnitID = 1,
                BaseSellingPrice = 100m,
                IsActive = true
            });
            context.ProductUnits.Add(new ProductUnit
            {
                ProductUnitID = 1,
                ProductID = 1,
                UnitID = 1,
                ConversionToBaseUnit = 1m,
                SellingPrice = 100m,
                IsDefaultSalesUnit = true,
                IsActive = true
            });
            await context.SaveChangesAsync();
        }

        private static ApplicationDbContext CreateContext(string name)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(name)
                .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            return new ApplicationDbContext(options);
        }
    }
}
