using System;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using InventorySystem.Models.Entities;

namespace InventorySystem.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // =========================================================
        // MASTER DATA
        // =========================================================
        public DbSet<Role> Roles { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<UserCompany> UserCompanies { get; set; }
        public DbSet<ApplicationModule> ApplicationModules { get; set; }
        public DbSet<ApplicationPage> ApplicationPages { get; set; }
        public DbSet<RolePermission> RolePermissions { get; set; }
        public DbSet<Company> Companies { get; set; }
        public DbSet<Area> Areas { get; set; }
        public DbSet<SubArea> SubAreas { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Warehouse> Warehouses { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Unit> Units { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<ProductUnit> ProductUnits { get; set; }
        public DbSet<Supplier> Suppliers { get; set; }
        public DbSet<Booker> Bookers { get; set; }

        // =========================================================
        // INVENTORY
        // =========================================================
        public DbSet<InventoryStock> InventoryStocks { get; set; }
        public DbSet<InventoryTransaction> InventoryTransactions { get; set; }

        // =========================================================
        // PURCHASING
        // =========================================================
        public DbSet<PurchaseInvoice> PurchaseInvoices { get; set; }
        public DbSet<PurchaseInvoiceItem> PurchaseInvoiceItems { get; set; }
        public DbSet<PurchaseReturn> PurchaseReturns { get; set; }
        public DbSet<PurchaseReturnItem> PurchaseReturnItems { get; set; }

        // =========================================================
        // QUOTATIONS & SALES
        // =========================================================
        public DbSet<Quotation> Quotations { get; set; }
        public DbSet<QuotationItem> QuotationItems { get; set; }
        public DbSet<SalesInvoice> SalesInvoices { get; set; }
        public DbSet<SalesInvoiceItem> SalesInvoiceItems { get; set; }
        public DbSet<InvoiceEditAudit> InvoiceEditAudits { get; set; }

        // =========================================================
        // PROMOTIONS & DISCOUNTS
        // =========================================================
        public DbSet<PromotionCampaign> PromotionCampaigns { get; set; }
        public DbSet<PromotionRule> PromotionRules { get; set; }
        public DbSet<InvoicePromotion> InvoicePromotions { get; set; }
        public DbSet<DiscountRule> DiscountRules { get; set; }
        public DbSet<InvoiceDiscount> InvoiceDiscounts { get; set; }

        // =========================================================
        // PAYMENTS
        // =========================================================
        public DbSet<CustomerPayment> CustomerPayments { get; set; }
        public DbSet<CompanyPayment> CompanyPayments { get; set; }
        public DbSet<ChequeStatusAudit> ChequeStatusAudits { get; set; }

        // =========================================================
        // LEDGERS
        // =========================================================
        public DbSet<CustomerLedger> CustomerLedgers { get; set; }
        public DbSet<CompanyLedger> CompanyLedgers { get; set; }

        // =========================================================
        // RETURNS
        // =========================================================
        public DbSet<SalesReturn> SalesReturns { get; set; }
        public DbSet<SalesReturnItem> SalesReturnItems { get; set; }

        // =========================================================
        // AUDIT
        // =========================================================
        public DbSet<AuditLog> AuditLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // =============================================================
            // GLOBAL SOFT DELETE QUERY FILTER
            // Automatically excludes IsDeleted=true from ALL queries on
            // entities implementing IHasIsDeleted.
            // Use .IgnoreQueryFilters() when you explicitly need deleted records.
            // =============================================================
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (typeof(IHasIsDeleted).IsAssignableFrom(entityType.ClrType))
                {
                    var parameter = Expression.Parameter(entityType.ClrType, "e");
                    var property = Expression.Property(parameter, nameof(IHasIsDeleted.IsDeleted));
                    var condition = Expression.Equal(property, Expression.Constant(false));
                    var lambda = Expression.Lambda(condition, parameter);
                    modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
                }
            }

            // =============================================================
            // ROLES
            // =============================================================
            modelBuilder.Entity<Role>(entity =>
            {
                entity.HasIndex(r => r.RoleName).IsUnique();

                // Role.CreatedBy → Users (self-ref via Users table, NoAction to avoid circular cascade)
                entity.HasOne(r => r.CreatedByUser)
                    .WithMany()
                    .HasForeignKey(r => r.CreatedBy)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(r => r.UpdatedByUser)
                    .WithMany()
                    .HasForeignKey(r => r.UpdatedBy)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            // =============================================================
            // PERMISSION MODULES & PAGES
            // =============================================================
            modelBuilder.Entity<ApplicationModule>(entity =>
            {
                entity.HasIndex(m => m.ModuleKey).IsUnique();
            });

            modelBuilder.Entity<ApplicationPage>(entity =>
            {
                entity.HasIndex(p => p.PageKey).IsUnique();

                entity.HasOne(p => p.Module)
                    .WithMany(m => m.Pages)
                    .HasForeignKey(p => p.ApplicationModuleID)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<RolePermission>(entity =>
            {
                entity.HasIndex(rp => new { rp.RoleID, rp.ApplicationPageID }).IsUnique();

                entity.HasOne(rp => rp.Role)
                    .WithMany(r => r.RolePermissions)
                    .HasForeignKey(rp => rp.RoleID)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(rp => rp.Page)
                    .WithMany(p => p.RolePermissions)
                    .HasForeignKey(rp => rp.ApplicationPageID)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // =============================================================
            // USERS
            // =============================================================
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasIndex(u => u.Username).IsUnique();

                // User → Role (NOT NULL FK — Restrict to prevent cascade)
                entity.HasOne(u => u.Role)
                    .WithMany(r => r.Users)
                    .HasForeignKey(u => u.RoleID)
                    .OnDelete(DeleteBehavior.Restrict);

                // Self-referencing: User.CreatedBy → Users (NoAction avoids multiple cascade paths)
                entity.HasOne(u => u.CreatedByUser)
                    .WithMany()
                    .HasForeignKey(u => u.CreatedBy)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(u => u.UpdatedByUser)
                    .WithMany()
                    .HasForeignKey(u => u.UpdatedBy)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            // =============================================================
            // USER COMPANIES (company access allow-list)
            // =============================================================
            modelBuilder.Entity<UserCompany>(entity =>
            {
                entity.HasIndex(uc => new { uc.UserID, uc.CompanyID }).IsUnique();

                entity.HasOne(uc => uc.User)
                    .WithMany(u => u.UserCompanies)
                    .HasForeignKey(uc => uc.UserID)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(uc => uc.Company)
                    .WithMany(c => c.UserCompanies)
                    .HasForeignKey(uc => uc.CompanyID)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(uc => uc.CreatedByUser)
                    .WithMany()
                    .HasForeignKey(uc => uc.CreatedBy)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(uc => uc.UpdatedByUser)
                    .WithMany()
                    .HasForeignKey(uc => uc.UpdatedBy)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            // =============================================================
            // COMPANIES
            // =============================================================
            modelBuilder.Entity<Company>(entity =>
            {
                entity.HasOne(c => c.UpdatedByUser)
                    .WithMany()
                    .HasForeignKey(c => c.UpdatedBy)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            // =============================================================
            // AREAS
            // =============================================================
            modelBuilder.Entity<Area>(entity =>
            {
                entity.HasIndex(a => a.AreaName).IsUnique();
                entity.HasIndex(a => a.Code).IsUnique();

                entity.HasOne(a => a.CreatedByUser)
                    .WithMany(u => u.CreatedAreas)
                    .HasForeignKey(a => a.CreatedBy)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(a => a.UpdatedByUser)
                    .WithMany()
                    .HasForeignKey(a => a.UpdatedBy)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            // =============================================================
            // SUB AREAS
            // =============================================================
            modelBuilder.Entity<SubArea>(entity =>
            {
                entity.HasOne(sa => sa.Area)
                    .WithMany(a => a.SubAreas)
                    .HasForeignKey(sa => sa.AreaID)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(sa => sa.CreatedByUser)
                    .WithMany()
                    .HasForeignKey(sa => sa.CreatedBy)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(sa => sa.UpdatedByUser)
                    .WithMany()
                    .HasForeignKey(sa => sa.UpdatedBy)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            // =============================================================
            // CUSTOMERS
            // =============================================================
            modelBuilder.Entity<Customer>(entity =>
            {
                entity.Property(c => c.PreferredDiscountPercent).HasPrecision(5, 2);

                entity.HasOne(c => c.Area)
                    .WithMany(a => a.Customers)
                    .HasForeignKey(c => c.AreaID)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(c => c.SubArea)
                    .WithMany(sa => sa.Customers)
                    .HasForeignKey(c => c.SubAreaID)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(c => c.CreatedByUser)
                    .WithMany(u => u.CreatedCustomers)
                    .HasForeignKey(c => c.CreatedBy)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(c => c.UpdatedByUser)
                    .WithMany()
                    .HasForeignKey(c => c.UpdatedBy)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            // =============================================================
            // WAREHOUSES
            // =============================================================
            modelBuilder.Entity<Warehouse>(entity =>
            {
                entity.HasOne(w => w.CreatedByUser)
                    .WithMany()
                    .HasForeignKey(w => w.CreatedBy)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(w => w.UpdatedByUser)
                    .WithMany()
                    .HasForeignKey(w => w.UpdatedBy)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            // =============================================================
            // CATEGORIES
            // =============================================================
            modelBuilder.Entity<Category>(entity =>
            {
                // Unique category name per company among non-deleted rows
                entity.HasIndex(c => new { c.CompanyID, c.Name })
                    .IsUnique()
                    .HasFilter("[IsDeleted] = 0");

                entity.HasOne(c => c.Company)
                    .WithMany(co => co.Categories)
                    .HasForeignKey(c => c.CompanyID)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(c => c.CreatedByUser)
                    .WithMany()
                    .HasForeignKey(c => c.CreatedBy)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(c => c.UpdatedByUser)
                    .WithMany()
                    .HasForeignKey(c => c.UpdatedBy)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            // =============================================================
            // BOOKERS
            // =============================================================
            modelBuilder.Entity<Booker>(entity =>
            {
                entity.HasIndex(b => b.CompanyID)
                    .HasDatabaseName("IX_Bookers_CompanyID");

                entity.HasIndex(b => new { b.CompanyID, b.Name })
                    .IsUnique()
                    .HasFilter("[IsDeleted] = 0")
                    .HasDatabaseName("UX_Bookers_CompanyID_Name");

                entity.HasIndex(b => b.CNIC)
                    .IsUnique()
                    .HasFilter("[IsDeleted] = 0 AND [CNIC] IS NOT NULL")
                    .HasDatabaseName("UX_Bookers_CNIC");

                entity.HasOne(b => b.Company)
                    .WithMany(c => c.Bookers)
                    .HasForeignKey(b => b.CompanyID)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(b => b.CreatedByUser)
                    .WithMany()
                    .HasForeignKey(b => b.CreatedBy)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(b => b.UpdatedByUser)
                    .WithMany()
                    .HasForeignKey(b => b.UpdatedBy)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            // =============================================================
            // UNITS
            // =============================================================
            modelBuilder.Entity<Unit>(entity =>
            {
                entity.HasIndex(u => u.UnitName).IsUnique();

                entity.HasOne(u => u.CreatedByUser)
                    .WithMany()
                    .HasForeignKey(u => u.CreatedBy)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(u => u.UpdatedByUser)
                    .WithMany()
                    .HasForeignKey(u => u.UpdatedBy)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            // =============================================================
            // PRODUCTS
            // =============================================================
            modelBuilder.Entity<Product>(entity =>
            {
                entity.HasIndex(p => p.SKU).IsUnique();
                entity.HasIndex(p => p.Barcode).IsUnique();

                entity.HasOne(p => p.Category)
                    .WithMany(c => c.Products)
                    .HasForeignKey(p => p.CategoryID)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(p => p.Company)
                    .WithMany(c => c.Products)
                    .HasForeignKey(p => p.CompanyID)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(p => p.BaseUnit)
                    .WithMany(u => u.ProductsAsBaseUnit)
                    .HasForeignKey(p => p.BaseUnitID)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(p => p.CreatedByUser)
                    .WithMany()
                    .HasForeignKey(p => p.CreatedBy)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(p => p.UpdatedByUser)
                    .WithMany()
                    .HasForeignKey(p => p.UpdatedBy)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            // =============================================================
            // PRODUCT UNITS
            // =============================================================
            modelBuilder.Entity<ProductUnit>(entity =>
            {
                entity.HasOne(pu => pu.Product)
                    .WithMany(p => p.ProductUnits)
                    .HasForeignKey(pu => pu.ProductID)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(pu => pu.Unit)
                    .WithMany(u => u.ProductUnits)
                    .HasForeignKey(pu => pu.UnitID)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(pu => pu.CreatedByUser)
                    .WithMany()
                    .HasForeignKey(pu => pu.CreatedBy)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(pu => pu.UpdatedByUser)
                    .WithMany()
                    .HasForeignKey(pu => pu.UpdatedBy)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            // =============================================================
            // INVENTORY STOCK
            // =============================================================
            modelBuilder.Entity<InventoryStock>(entity =>
            {
                // One record per Product-Warehouse pair
                entity.HasIndex(s => new { s.ProductID, s.WarehouseID }).IsUnique();

                entity.HasOne(s => s.Product)
                    .WithMany(p => p.InventoryStocks)
                    .HasForeignKey(s => s.ProductID)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(s => s.Warehouse)
                    .WithMany(w => w.InventoryStocks)
                    .HasForeignKey(s => s.WarehouseID)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(s => s.CreatedByUser)
                    .WithMany()
                    .HasForeignKey(s => s.CreatedBy)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(s => s.UpdatedByUser)
                    .WithMany()
                    .HasForeignKey(s => s.UpdatedBy)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            // =============================================================
            // INVENTORY TRANSACTIONS
            // =============================================================
            modelBuilder.Entity<InventoryTransaction>(entity =>
            {
                entity.HasOne(t => t.Product)
                    .WithMany(p => p.InventoryTransactions)
                    .HasForeignKey(t => t.ProductID)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(t => t.Warehouse)
                    .WithMany(w => w.InventoryTransactions)
                    .HasForeignKey(t => t.WarehouseID)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(t => t.PurchaseInvoiceItem)
                    .WithMany(pi => pi.InventoryTransactions)
                    .HasForeignKey(t => t.PurchaseInvoiceItemID)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(t => t.SalesInvoiceItem)
                    .WithMany(si => si.InventoryTransactions)
                    .HasForeignKey(t => t.SalesInvoiceItemID)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(t => t.SalesReturnItem)
                    .WithMany(sr => sr.InventoryTransactions)
                    .HasForeignKey(t => t.SalesReturnItemID)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(t => t.CreatedByUser)
                    .WithMany()
                    .HasForeignKey(t => t.CreatedBy)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(t => t.UpdatedByUser)
                    .WithMany()
                    .HasForeignKey(t => t.UpdatedBy)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            // =============================================================
            // PURCHASE INVOICES
            // =============================================================
            modelBuilder.Entity<PurchaseInvoice>(entity =>
            {
                entity.HasIndex(pi => pi.InvoiceNumber).IsUnique();

                entity.HasOne(pi => pi.Company)
                    .WithMany(c => c.PurchaseInvoices)
                    .HasForeignKey(pi => pi.CompanyID)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(pi => pi.Warehouse)
                    .WithMany(w => w.PurchaseInvoices)
                    .HasForeignKey(pi => pi.WarehouseID)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(pi => pi.CreatedByUser)
                    .WithMany(u => u.PurchaseInvoices)
                    .HasForeignKey(pi => pi.CreatedBy)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            // =============================================================
            // PURCHASE INVOICE ITEMS
            // =============================================================
            modelBuilder.Entity<PurchaseInvoiceItem>(entity =>
            {
                entity.HasOne(pii => pii.PurchaseInvoice)
                    .WithMany(pi => pi.Items)
                    .HasForeignKey(pii => pii.PurchaseInvoiceID)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(pii => pii.Product)
                    .WithMany(p => p.PurchaseInvoiceItems)
                    .HasForeignKey(pii => pii.ProductID)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(pii => pii.ProductUnit)
                    .WithMany(pu => pu.PurchaseInvoiceItems)
                    .HasForeignKey(pii => pii.ProductUnitID)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(pii => pii.CreatedByUser)
                    .WithMany()
                    .HasForeignKey(pii => pii.CreatedBy)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(pii => pii.UpdatedByUser)
                    .WithMany()
                    .HasForeignKey(pii => pii.UpdatedBy)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            // =============================================================
            // QUOTATIONS
            // =============================================================
            modelBuilder.Entity<Quotation>(entity =>
            {
                entity.HasOne(q => q.Customer)
                    .WithMany(c => c.Quotations)
                    .HasForeignKey(q => q.CustomerID)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(q => q.CreatedByUser)
                    .WithMany()
                    .HasForeignKey(q => q.CreatedBy)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(q => q.UpdatedByUser)
                    .WithMany()
                    .HasForeignKey(q => q.UpdatedBy)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            // =============================================================
            // QUOTATION ITEMS
            // =============================================================
            modelBuilder.Entity<QuotationItem>(entity =>
            {
                entity.HasOne(qi => qi.Quotation)
                    .WithMany(q => q.Items)
                    .HasForeignKey(qi => qi.QuotationID)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(qi => qi.Product)
                    .WithMany(p => p.QuotationItems)
                    .HasForeignKey(qi => qi.ProductID)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(qi => qi.ProductUnit)
                    .WithMany(pu => pu.QuotationItems)
                    .HasForeignKey(qi => qi.ProductUnitID)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(qi => qi.CreatedByUser)
                    .WithMany()
                    .HasForeignKey(qi => qi.CreatedBy)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(qi => qi.UpdatedByUser)
                    .WithMany()
                    .HasForeignKey(qi => qi.UpdatedBy)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            // =============================================================
            // SALES INVOICES
            // =============================================================
            modelBuilder.Entity<SalesInvoice>(entity =>
            {
                entity.HasIndex(si => si.InvoiceNumber).IsUnique();

                entity.Property(si => si.DiscountMode).HasMaxLength(20);

                entity.HasOne(si => si.Customer)
                    .WithMany(c => c.SalesInvoices)
                    .HasForeignKey(si => si.CustomerID)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(si => si.Company)
                    .WithMany(c => c.SalesInvoices)
                    .HasForeignKey(si => si.CompanyID)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(si => si.Booker)
                    .WithMany(b => b.SalesInvoices)
                    .HasForeignKey(si => si.BookerID)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(si => si.SalespersonUser)
                    .WithMany()
                    .HasForeignKey(si => si.SalespersonID)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasIndex(si => si.CompanyID);
                entity.HasIndex(si => si.BookerID);
                entity.HasIndex(si => si.SalespersonID);

                entity.HasOne(si => si.Warehouse)
                    .WithMany(w => w.SalesInvoices)
                    .HasForeignKey(si => si.WarehouseID)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(si => si.Area)
                    .WithMany(a => a.SalesInvoices)
                    .HasForeignKey(si => si.AreaID)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(si => si.SubArea)
                    .WithMany(sa => sa.SalesInvoices)
                    .HasForeignKey(si => si.SubAreaID)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(si => si.Supplier)
                    .WithMany(s => s.SalesInvoices)
                    .HasForeignKey(si => si.SupplierID)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(si => si.Quotation)
                    .WithMany(q => q.SalesInvoices)
                    .HasForeignKey(si => si.QuotationID)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(si => si.CreatedByUser)
                    .WithMany(u => u.SalesInvoices)
                    .HasForeignKey(si => si.CreatedBy)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(si => si.UpdatedByUser)
                    .WithMany()
                    .HasForeignKey(si => si.UpdatedBy)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            // =============================================================
            // SALES INVOICE ITEMS
            // =============================================================
            modelBuilder.Entity<SalesInvoiceItem>(entity =>
            {
                entity.Property(sii => sii.DiscountRate).HasColumnType("decimal(18,4)");

                entity.HasOne(sii => sii.SalesInvoice)
                    .WithMany(si => si.Items)
                    .HasForeignKey(sii => sii.InvoiceID)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(sii => sii.Product)
                    .WithMany(p => p.SalesInvoiceItems)
                    .HasForeignKey(sii => sii.ProductID)
                    .IsRequired(false)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(sii => sii.ProductUnit)
                    .WithMany(pu => pu.SalesInvoiceItems)
                    .HasForeignKey(sii => sii.ProductUnitID)
                    .IsRequired(false)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(sii => sii.PromotionCampaign)
                    .WithMany(pc => pc.AppliedInvoiceItems)
                    .HasForeignKey(sii => sii.PromotionID)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(sii => sii.CreatedByUser)
                    .WithMany()
                    .HasForeignKey(sii => sii.CreatedBy)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(sii => sii.UpdatedByUser)
                    .WithMany()
                    .HasForeignKey(sii => sii.UpdatedBy)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            // =============================================================
            // PROMOTION CAMPAIGNS
            // =============================================================
            modelBuilder.Entity<PromotionCampaign>(entity =>
            {
                entity.HasOne(pc => pc.Company)
                    .WithMany(c => c.PromotionCampaigns)
                    .HasForeignKey(pc => pc.CompanyID)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(pc => pc.CompanyID);

                entity.HasOne(pc => pc.CreatedByUser)
                    .WithMany()
                    .HasForeignKey(pc => pc.CreatedBy)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            // =============================================================
            // PROMOTION RULES
            // NoAction on both product FKs to prevent multiple cascade paths in SQL Server
            // =============================================================
            modelBuilder.Entity<PromotionRule>(entity =>
            {
                entity.HasOne(pr => pr.PromotionCampaign)
                    .WithMany(pc => pc.PromotionRules)
                    .HasForeignKey(pr => pr.PromotionID)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(pr => pr.BuyProduct)
                    .WithMany(p => p.BuyPromotionRules)
                    .HasForeignKey(pr => pr.BuyProductID)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(pr => pr.FreeProduct)
                    .WithMany(p => p.FreePromotionRules)
                    .HasForeignKey(pr => pr.FreeProductID)
                    .IsRequired(false)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.Property(pr => pr.CustomFreeItemName).HasMaxLength(200);
            });

            // =============================================================
            // INVOICE PROMOTIONS (snapshot)
            // =============================================================
            modelBuilder.Entity<InvoicePromotion>(entity =>
            {
                entity.HasOne(ip => ip.SalesInvoice)
                    .WithMany(si => si.InvoicePromotions)
                    .HasForeignKey(ip => ip.InvoiceID)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(ip => ip.PromotionCampaign)
                    .WithMany(pc => pc.InvoicePromotions)
                    .HasForeignKey(ip => ip.PromotionID)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(ip => ip.AppliedByUser)
                    .WithMany()
                    .HasForeignKey(ip => ip.AppliedBy)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            // =============================================================
            // CUSTOMER PAYMENTS (BR-019: InvoiceID NOT NULL)
            // =============================================================
            modelBuilder.Entity<CustomerPayment>(entity =>
            {
                entity.HasOne(cp => cp.Customer)
                    .WithMany(c => c.CustomerPayments)
                    .HasForeignKey(cp => cp.CustomerID)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(cp => cp.SalesInvoice)
                    .WithMany(si => si.CustomerPayments)
                    .HasForeignKey(cp => cp.InvoiceID)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(cp => cp.ReceivedByUser)
                    .WithMany(u => u.ReceivedPayments)
                    .HasForeignKey(cp => cp.ReceivedBy)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            // =============================================================
            // COMPANY PAYMENTS (BR-023: PurchaseInvoiceID NOT NULL)
            // =============================================================
            modelBuilder.Entity<CompanyPayment>(entity =>
            {
                entity.HasOne(cp => cp.Company)
                    .WithMany(c => c.CompanyPayments)
                    .HasForeignKey(cp => cp.CompanyID)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(cp => cp.PurchaseInvoice)
                    .WithMany(pi => pi.CompanyPayments)
                    .HasForeignKey(cp => cp.PurchaseInvoiceID)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(cp => cp.PaidByUser)
                    .WithMany()
                    .HasForeignKey(cp => cp.PaidBy)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            // =============================================================
            // CUSTOMER LEDGER (immutable audit — no soft delete)
            // =============================================================
            modelBuilder.Entity<CustomerLedger>(entity =>
            {
                entity.HasOne(cl => cl.Customer)
                    .WithMany(c => c.LedgerEntries)
                    .HasForeignKey(cl => cl.CustomerID)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(cl => cl.SalesInvoice)
                    .WithMany(si => si.LedgerEntries)
                    .HasForeignKey(cl => cl.SalesInvoiceID)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(cl => cl.CustomerPayment)
                    .WithOne(cp => cp.CustomerLedgerEntry)
                    .HasForeignKey<CustomerLedger>(cl => cl.CustomerPaymentID)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(cl => cl.SalesReturn)
                    .WithMany(sr => sr.LedgerEntries)
                    .HasForeignKey(cl => cl.SalesReturnID)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(cl => cl.CreatedByUser)
                    .WithMany()
                    .HasForeignKey(cl => cl.CreatedBy)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            // =============================================================
            // SALES RETURNS & ITEMS (Optional Foreign Keys Configuration)
            // =============================================================
            modelBuilder.Entity<SalesReturn>(entity =>
            {
                entity.HasOne(sr => sr.SalesInvoice)
                    .WithMany(si => si.SalesReturns)
                    .HasForeignKey(sr => sr.InvoiceID)
                    .IsRequired(false)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(sr => sr.Warehouse)
                    .WithMany()
                    .HasForeignKey(sr => sr.WarehouseID)
                    .IsRequired(false)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<SalesReturnItem>(entity =>
            {
                entity.HasOne(sri => sri.SalesInvoiceItem)
                    .WithMany(sii => sii.SalesReturnItems)
                    .HasForeignKey(sri => sri.InvoiceItemID)
                    .IsRequired(false)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            // =============================================================
            // COMPANY LEDGER (immutable audit — no soft delete)
            // =============================================================
            modelBuilder.Entity<CompanyLedger>(entity =>
            {
                entity.HasOne(cl => cl.Company)
                    .WithMany(c => c.CompanyLedgerEntries)
                    .HasForeignKey(cl => cl.CompanyID)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(cl => cl.PurchaseInvoice)
                    .WithMany(pi => pi.CompanyLedgerEntries)
                    .HasForeignKey(cl => cl.PurchaseInvoiceID)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(cl => cl.CompanyPayment)
                    .WithOne(cp => cp.CompanyLedgerEntry)
                    .HasForeignKey<CompanyLedger>(cl => cl.CompanyPaymentID)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(cl => cl.CreatedByUser)
                    .WithMany()
                    .HasForeignKey(cl => cl.CreatedBy)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            // =============================================================
            // SALES RETURNS
            // =============================================================
            modelBuilder.Entity<SalesReturn>(entity =>
            {
                entity.HasOne(sr => sr.SalesInvoice)
                    .WithMany(si => si.SalesReturns)
                    .HasForeignKey(sr => sr.InvoiceID)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(sr => sr.Customer)
                    .WithMany(c => c.SalesReturns)
                    .HasForeignKey(sr => sr.CustomerID)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(sr => sr.CreatedByUser)
                    .WithMany()
                    .HasForeignKey(sr => sr.CreatedBy)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            // =============================================================
            // SALES RETURN ITEMS
            // =============================================================
            modelBuilder.Entity<SalesReturnItem>(entity =>
            {
                entity.Property(sri => sri.DiscountAmount).HasColumnType("decimal(18,2)");

                entity.HasOne(sri => sri.SalesReturn)
                    .WithMany(sr => sr.Items)
                    .HasForeignKey(sri => sri.SalesReturnID)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(sri => sri.SalesInvoiceItem)
                    .WithMany(sii => sii.SalesReturnItems)
                    .HasForeignKey(sri => sri.InvoiceItemID)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(sri => sri.Product)
                    .WithMany(p => p.SalesReturnItems)
                    .HasForeignKey(sri => sri.ProductID)
                    .IsRequired(false)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(sri => sri.ProductUnit)
                    .WithMany(pu => pu.SalesReturnItems)
                    .HasForeignKey(sri => sri.ProductUnitID)
                    .IsRequired(false)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // =============================================================
            // AUDIT LOGS (immutable — no soft delete, no cascade)
            // =============================================================
            modelBuilder.Entity<AuditLog>(entity =>
            {
                entity.HasOne(al => al.User)
                    .WithMany(u => u.AuditLogs)
                    .HasForeignKey(al => al.UserID)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            // =============================================================
            // DISCOUNT RULES
            // =============================================================
            modelBuilder.Entity<DiscountRule>(entity =>
            {
                entity.HasOne(dr => dr.Company)
                    .WithMany(c => c.DiscountRules)
                    .HasForeignKey(dr => dr.CompanyID)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(dr => dr.CompanyID);

                entity.HasOne(dr => dr.CreatedByUser)
                    .WithMany()
                    .HasForeignKey(dr => dr.CreatedBy)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            // =============================================================
            // INVOICE DISCOUNTS (snapshot)
            // =============================================================
            modelBuilder.Entity<InvoiceDiscount>(entity =>
            {
                entity.HasOne(id => id.SalesInvoice)
                    .WithMany(si => si.InvoiceDiscounts)
                    .HasForeignKey(id => id.InvoiceID)
                    .OnDelete(DeleteBehavior.Restrict);

                // Nullable: Manual discounts have no DiscountRuleID.
                entity.HasOne(id => id.DiscountRule)
                    .WithMany(dr => dr.InvoiceDiscounts)
                    .HasForeignKey(id => id.DiscountRuleID)
                    .IsRequired(false)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.Property(id => id.DiscountSource).HasMaxLength(20);
                entity.Property(id => id.MinimumOrderAmount).HasColumnType("decimal(18,2)");
                entity.Property(id => id.MaximumOrderAmount).HasColumnType("decimal(18,2)");

                entity.HasOne(id => id.AppliedByUser)
                    .WithMany()
                    .HasForeignKey(id => id.AppliedBy)
                    .OnDelete(DeleteBehavior.NoAction);
            });
        }
    }
}
