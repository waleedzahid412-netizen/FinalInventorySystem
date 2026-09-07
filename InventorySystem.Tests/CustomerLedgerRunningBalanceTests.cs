using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using InventorySystem.Data;
using InventorySystem.Models.Entities;
using InventorySystem.Repositories.Implementations;
using Xunit;

namespace InventorySystem.Tests
{
    public class CustomerLedgerRunningBalanceTests
    {
        private static ApplicationDbContext CreateDb(string name)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: name)
                .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            return new ApplicationDbContext(options);
        }

        private static async Task SeedCustomerAsync(ApplicationDbContext db)
        {
            db.Roles.Add(new Role { RoleID = 1, RoleName = "Admin", IsActive = true });
            db.Users.Add(new User { UserID = 1, RoleID = 1, FullName = "Admin", Username = "admin", PasswordHash = "x", IsActive = true });
            db.Customers.Add(new Customer { CustomerID = 1, ShopName = "Test Shop", IsActive = true, CreditLimit = 100000m });
            await db.SaveChangesAsync();
        }

        [Fact]
        public async Task GetLedgerAsync_MixedSalesAndPayment_ComputesRunningBalance_NewestFirst()
        {
            await using var db = CreateDb(nameof(GetLedgerAsync_MixedSalesAndPayment_ComputesRunningBalance_NewestFirst));
            await SeedCustomerAsync(db);

            var baseDate = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);
            db.CustomerLedgers.AddRange(
                new CustomerLedger
                {
                    CustomerLedgerID = 1,
                    CustomerID = 1,
                    TransactionDate = baseDate,
                    TransactionType = "SALE",
                    DebitAmount = 15m,
                    CreditAmount = 0m,
                    CreatedBy = 1
                },
                new CustomerLedger
                {
                    CustomerLedgerID = 2,
                    CustomerID = 1,
                    TransactionDate = baseDate.AddHours(1),
                    TransactionType = "PAYMENT",
                    DebitAmount = 0m,
                    CreditAmount = 3m,
                    CreatedBy = 1
                },
                new CustomerLedger
                {
                    CustomerLedgerID = 3,
                    CustomerID = 1,
                    TransactionDate = baseDate.AddHours(2),
                    TransactionType = "SALE",
                    DebitAmount = 20m,
                    CreditAmount = 0m,
                    CreatedBy = 1
                });
            await db.SaveChangesAsync();

            var repo = new CustomerRepository(db);
            var result = await repo.GetLedgerAsync(1, 1, 10);

            Assert.Equal(3, result.TotalCount);
            Assert.Equal(3, result.Items[0].CustomerLedgerID);
            Assert.Equal(32m, result.Items[0].RunningBalance);
            Assert.Equal(2, result.Items[1].CustomerLedgerID);
            Assert.Equal(12m, result.Items[1].RunningBalance);
            Assert.Equal(1, result.Items[2].CustomerLedgerID);
            Assert.Equal(15m, result.Items[2].RunningBalance);
        }

        [Fact]
        public async Task GetLedgerAsync_ReturnScenario_ComputesRunningBalanceAndSplitKpis()
        {
            await using var db = CreateDb(nameof(GetLedgerAsync_ReturnScenario_ComputesRunningBalanceAndSplitKpis));
            await SeedCustomerAsync(db);

            var baseDate = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);
            db.CustomerLedgers.AddRange(
                new CustomerLedger
                {
                    CustomerLedgerID = 5,
                    CustomerID = 1,
                    TransactionDate = baseDate,
                    TransactionType = "SALE",
                    DebitAmount = 63m,
                    CreditAmount = 0m,
                    CreatedBy = 1
                },
                new CustomerLedger
                {
                    CustomerLedgerID = 6,
                    CustomerID = 1,
                    TransactionDate = baseDate.AddHours(1),
                    TransactionType = "PAYMENT",
                    DebitAmount = 0m,
                    CreditAmount = 15m,
                    CreatedBy = 1
                },
                new CustomerLedger
                {
                    CustomerLedgerID = 7,
                    CustomerID = 1,
                    TransactionDate = baseDate.AddHours(2),
                    TransactionType = "RETURN",
                    DebitAmount = 0m,
                    CreditAmount = 63m,
                    CreatedBy = 1
                });
            await db.SaveChangesAsync();

            var repo = new CustomerRepository(db);
            var ledger = await repo.GetLedgerAsync(1, 1, 10);
            var summary = await repo.GetFinancialSummaryAsync(1);

            Assert.NotNull(summary);
            Assert.Equal(-15m, summary!.OutstandingReceivable);
            Assert.Equal(0m, summary.CustomerReceivable);
            Assert.Equal(15m, summary.AmountOwedToCustomer);

            Assert.Equal(7, ledger.Items[0].CustomerLedgerID);
            Assert.Equal(-15m, ledger.Items[0].RunningBalance);
            Assert.Equal(6, ledger.Items[1].CustomerLedgerID);
            Assert.Equal(48m, ledger.Items[1].RunningBalance);
            Assert.Equal(5, ledger.Items[2].CustomerLedgerID);
            Assert.Equal(63m, ledger.Items[2].RunningBalance);
        }

        [Fact]
        public async Task CustomerFinancialSummary_PositiveNet_SplitsReceivableOnly()
        {
            await using var db = CreateDb(nameof(CustomerFinancialSummary_PositiveNet_SplitsReceivableOnly));
            await SeedCustomerAsync(db);

            db.CustomerLedgers.Add(new CustomerLedger
            {
                CustomerID = 1,
                TransactionDate = DateTime.UtcNow,
                TransactionType = "SALE",
                DebitAmount = 48m,
                CreditAmount = 0m,
                CreatedBy = 1
            });
            await db.SaveChangesAsync();

            var summary = await new CustomerRepository(db).GetFinancialSummaryAsync(1);

            Assert.NotNull(summary);
            Assert.Equal(48m, summary!.OutstandingReceivable);
            Assert.Equal(48m, summary.CustomerReceivable);
            Assert.Equal(0m, summary.AmountOwedToCustomer);
        }
    }
}
