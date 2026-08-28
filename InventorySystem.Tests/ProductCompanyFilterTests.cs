using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using InventorySystem.Data;
using InventorySystem.DTOs.Products;
using InventorySystem.Models.Entities;
using InventorySystem.Repositories.Implementations;
using Xunit;

namespace InventorySystem.Tests
{
    public class ProductCompanyFilterTests
    {
        private static ApplicationDbContext CreateDb(string name)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: name)
                .Options;
            return new ApplicationDbContext(options);
        }

        [Fact]
        public async Task GetPagedAsync_FiltersByCompanyID()
        {
            await using var db = CreateDb(nameof(GetPagedAsync_FiltersByCompanyID));
            db.Companies.AddRange(
                new Company { CompanyID = 1, CompanyName = "A" },
                new Company { CompanyID = 2, CompanyName = "B" });
            db.Categories.Add(new Category { CategoryID = 1, CompanyID = 1, Name = "Cat" });
            db.Units.Add(new Unit { UnitID = 1, UnitName = "PCS" });
            db.Products.AddRange(
                new Product { ProductID = 1, ProductName = "P1", CompanyID = 1, CategoryID = 1, BaseUnitID = 1, IsActive = true },
                new Product { ProductID = 2, ProductName = "P2", CompanyID = 2, CategoryID = 1, BaseUnitID = 1, IsActive = true });
            await db.SaveChangesAsync();

            var repo = new ProductRepository(db);
            var page = await repo.GetPagedAsync(new ProductFilterDto { CompanyID = 1, PageNumber = 1, PageSize = 50 });

            Assert.Single(page.Items);
            Assert.Equal(1, page.Items[0].CompanyID);
        }
    }
}
