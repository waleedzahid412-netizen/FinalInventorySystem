using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BCrypt.Net;
using Microsoft.EntityFrameworkCore;
using InventorySystem.Constants;
using InventorySystem.Models.Entities;

namespace InventorySystem.Data
{
    public static class DbInitializer
    {
        public static async Task SeedAsync(ApplicationDbContext context)
        {
            try
            {
                // ===== 1. SEED ROLES =====
                var adminRole = await context.Roles
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(r => r.RoleName == "Admin");

                if (adminRole == null)
                {
                    adminRole = new Role
                    {
                        RoleName = "Admin",
                        RoleDescription = "System administrator with full access",
                        IsSystemRole = true,
                        IsActive = true,
                        IsDeleted = false,
                        CreatedAt = DateTime.UtcNow
                    };

                    await context.Roles.AddAsync(adminRole);
                    await context.SaveChangesAsync();
                }
                else if (!adminRole.IsSystemRole)
                {
                    adminRole.IsSystemRole = true;
                    adminRole.RoleDescription ??= "System administrator with full access";
                    await context.SaveChangesAsync();
                }

                var userRole = await context.Roles
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(r => r.RoleName == "User");

                if (userRole == null)
                {
                    userRole = new Role
                    {
                        RoleName = "User",
                        RoleDescription = "Standard user with view-only defaults",
                        IsSystemRole = false,
                        IsActive = true,
                        IsDeleted = false,
                        CreatedAt = DateTime.UtcNow
                    };

                    await context.Roles.AddAsync(userRole);
                    await context.SaveChangesAsync();
                }

                await SeedPermissionCatalogAsync(context);
                await SeedDefaultUserRolePermissionsAsync(context, userRole.RoleID);

                // ===== 2. SEED ADMIN USER =====
                var adminUser = await context.Users
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(u => u.Username == "admin");

                if (adminUser == null)
                {
                    adminUser = new User
                    {
                        RoleID = adminRole.RoleID,
                        FullName = "System Administrator",
                        Username = "admin",
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                        IsActive = true,
                        IsDeleted = false,
                        CreatedAt = DateTime.UtcNow
                    };

                    await context.Users.AddAsync(adminUser);
                    await context.SaveChangesAsync();
                }

                var salespersonUsers = await SeedSalespersonUsersAsync(context, adminRole.RoleID, adminUser.UserID);

                // ===== 3. SEED UNITS =====
                var unitsDict = await SeedUnitsAsync(context);

                // ===== 4. SEED COMPANIES =====
                var companiesDict = await SeedCompaniesAsync(context);

                // ===== 5. SEED CATEGORIES (company-scoped) =====
                var categoriesDict = await SeedCategoriesAsync(context, companiesDict);

                // ===== 6. SEED PRODUCTS & PRODUCT UNITS =====
                await SeedProductsAndUnitsAsync(context, adminUser.UserID, categoriesDict, unitsDict, companiesDict);

                // ===== 7. SEED AREAS & SUB-AREAS =====
                var (areasDict, subAreasDict) = await SeedAreasAndSubAreasAsync(context);

                // ===== 8. SEED CUSTOMERS =====
                await SeedCustomersAsync(context, adminUser.UserID, areasDict, subAreasDict);

                // ===== 9. SEED WAREHOUSES =====
                var warehouses = await SeedWarehousesAsync(context, adminUser.UserID);

                // ===== 10. SEED PURCHASE INVOICES =====
                await SeedPurchaseInvoicesAsync(context, adminUser.UserID, warehouses);

                // ===== 10b. SEED BOOKERS (company-scoped — BR-043) =====
                var bookers = await SeedBookersAsync(context, companiesDict);

                // ===== 10c. SEED SUPPLIERS =====
                var suppliers = await SeedSuppliersAsync(context);

                // ===== 11. SEED SALES INVOICES =====
                await SeedSalesInvoicesAsync(context, adminUser.UserID, warehouses, bookers, suppliers);

                // ===== 11b. BACKFILL EXISTING INVOICES FOR LOAD SHEET TESTING =====
                await BackfillExistingInvoicesForLoadSheetTestingAsync(context, bookers, salespersonUsers, suppliers);

                // ===== 12. SEED PROMOTIONS & DISCOUNTS =====
                await SeedPromotionsAndDiscountsAsync(context, adminUser.UserID);

                Console.WriteLine("=================================================");
                Console.WriteLine("✅ Database seeded successfully!");
                Console.WriteLine("   Categories: company-scoped | Units: 7 | Companies: 40 | Products: 50 | Areas: 7 | Customers: 42 | Warehouses: 3 | Purchase Invoices: 6 | Sales Invoices: 4 | Promos & Discounts: Active");
                Console.WriteLine("   Default Admin Username: admin");
                Console.WriteLine("   Default Password: admin123");
                Console.WriteLine("=================================================");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Seeding error: {ex.Message}");
                throw;
            }
        }

        private static async Task<Dictionary<(int CompanyId, string Name), Category>> SeedCategoriesAsync(
            ApplicationDbContext context,
            Dictionary<string, Company> companies)
        {
            var existingCategories = await context.Categories.IgnoreQueryFilters().ToListAsync();
            if (existingCategories.Any())
            {
                return existingCategories.ToDictionary(
                    c => (c.CompanyID, c.Name),
                    c => c);
            }

            var standardNames = new[]
            {
                "Beverages",
                "Snacks & Confectionery",
                "Dairy & Milk Products",
                "Cooking Oil & Ghee",
                "Rice & Grains",
                "Spices & Recipe Mixes",
                "Tea & Coffee",
                "Jams, Sauces & Condiments",
                "Personal Care & Hygiene",
                "Household & Cleaning"
            };

            var now = DateTime.UtcNow;
            var categories = new List<Category>();

            foreach (var company in companies.Values)
            {
                foreach (var name in standardNames)
                {
                    categories.Add(new Category
                    {
                        CompanyID = company.CompanyID,
                        Name = name,
                        IsActive = true,
                        IsDeleted = false,
                        CreatedAt = now
                    });
                }
            }

            await context.Categories.AddRangeAsync(categories);
            await context.SaveChangesAsync();

            return categories.ToDictionary(c => (c.CompanyID, c.Name), c => c);
        }

        private static async Task<Dictionary<string, Unit>> SeedUnitsAsync(ApplicationDbContext context)
        {
            var existingUnits = await context.Units.IgnoreQueryFilters().ToListAsync();
            if (existingUnits.Any())
            {
                return existingUnits.ToDictionary(u => u.UnitName, u => u);
            }

            var units = new List<Unit>
            {
                new Unit { UnitName = "Piece", IsActive = true, IsDeleted = false, CreatedAt = DateTime.UtcNow },
                new Unit { UnitName = "Bottle", IsActive = true, IsDeleted = false, CreatedAt = DateTime.UtcNow },
                new Unit { UnitName = "Can", IsActive = true, IsDeleted = false, CreatedAt = DateTime.UtcNow },
                new Unit { UnitName = "Packet", IsActive = true, IsDeleted = false, CreatedAt = DateTime.UtcNow },
                new Unit { UnitName = "Box", IsActive = true, IsDeleted = false, CreatedAt = DateTime.UtcNow },
                new Unit { UnitName = "Carton", IsActive = true, IsDeleted = false, CreatedAt = DateTime.UtcNow },
                new Unit { UnitName = "Bag", IsActive = true, IsDeleted = false, CreatedAt = DateTime.UtcNow }
            };

            await context.Units.AddRangeAsync(units);
            await context.SaveChangesAsync();

            return units.ToDictionary(u => u.UnitName, u => u);
        }

        private static async Task<Dictionary<string, Company>> SeedCompaniesAsync(ApplicationDbContext context)
        {
            var existingCompanies = await context.Companies.IgnoreQueryFilters().ToListAsync();
            if (existingCompanies.Any())
            {
                return existingCompanies.ToDictionary(c => c.CompanyName, c => c);
            }

            var now = DateTime.UtcNow;

            var companies = new List<Company>
            {
                // ===== BEVERAGES =====
                new Company { CompanyName = "The Coca-Cola Company", ContactPerson = "Ahmed Raza", Phone = "0300-1234567", Email = "orders@coca-cola.com.pk", Address = "Industrial Estate, Kot Lakhpat, Lahore", TaxID = "NTN-CC-00001", IsDeleted = false, CreatedAt = now },
                new Company { CompanyName = "PepsiCo Beverages Ltd", ContactPerson = "Bilal Hussain", Phone = "0301-2345678", Email = "orders@pepsico.com.pk", Address = "Multan Road, Lahore", TaxID = "NTN-PEP-00002", IsDeleted = false, CreatedAt = now },
                new Company { CompanyName = "Nestlé Pakistan Ltd", ContactPerson = "Sara Khan", Phone = "0302-3456789", Email = "supply@nestle.pk", Address = "308 Upper Mall, Lahore", TaxID = "NTN-NES-00003", IsDeleted = false, CreatedAt = now },

                // ===== FMCG / PERSONAL CARE =====
                new Company { CompanyName = "Unilever FMCG Pakistan", ContactPerson = "Farhan Ali", Phone = "0303-4567890", Email = "corporate@unilever.com.pk", Address = "Avari Plaza, Fatima Jinnah Road, Karachi", TaxID = "NTN-UNI-00004", IsDeleted = false, CreatedAt = now },
                new Company { CompanyName = "Procter & Gamble (P&G)", ContactPerson = "Hina Javed", Phone = "0304-5678901", Email = "sales@pg.com.pk", Address = "Ground Floor, Harbour Front, Karachi", TaxID = "NTN-PNG-00005", IsDeleted = false, CreatedAt = now },
                new Company { CompanyName = "Colgate-Palmolive Pakistan", ContactPerson = "Usman Tariq", Phone = "0305-6789012", Email = "orders@colgatepalmolive.pk", Address = "Lakson Square, Sarwar Shaheed Road, Karachi", TaxID = "NTN-CPP-00006", IsDeleted = false, CreatedAt = now },
                new Company { CompanyName = "Reckitt Benckiser Pakistan", ContactPerson = "Asma Khalid", Phone = "0306-7890123", Email = "supply@reckitt.pk", Address = "4th Floor, The Forum, Clifton, Karachi", TaxID = "NTN-RBP-00007", IsDeleted = false, CreatedAt = now },

                // ===== SPICES & RECIPE MIXES =====
                new Company { CompanyName = "National Foods Limited", ContactPerson = "Zubair Ahmed", Phone = "0307-8901234", Email = "info@nfoods.com", Address = "F-133, S.I.T.E., Karachi", TaxID = "NTN-NFL-00008", IsDeleted = false, CreatedAt = now },
                new Company { CompanyName = "Shan Foods (Pvt) Ltd", ContactPerson = "Nadia Malik", Phone = "0308-9012345", Email = "sales@shanfoods.com", Address = "Sector 15, Korangi Industrial Area, Karachi", TaxID = "NTN-SHN-00009", IsDeleted = false, CreatedAt = now },
                new Company { CompanyName = "Mehran Spice & Food Industries", ContactPerson = "Kamran Shah", Phone = "0309-0123456", Email = "sales@mehranfoods.com", Address = "Hub Chowki, Lasbela, Balochistan", TaxID = "NTN-MEH-00010", IsDeleted = false, CreatedAt = now },

                // ===== TEA & COFFEE =====
                new Company { CompanyName = "Tapal Tea (Pvt) Ltd", ContactPerson = "Saeed Akhtar", Phone = "0310-1234567", Email = "trade@tapaltea.com", Address = "Plot 40, Sector 15, Korangi, Karachi", TaxID = "NTN-TAP-00011", IsDeleted = false, CreatedAt = now },
                new Company { CompanyName = "Vital Tea Pakistan", ContactPerson = "Imran Abbas", Phone = "0311-2345678", Email = "orders@vitaltea.com.pk", Address = "Industrial Area, Hattar, KPK", TaxID = "NTN-VIT-00012", IsDeleted = false, CreatedAt = now },

                // ===== DAIRY =====
                new Company { CompanyName = "Engro Foods (Olper's)", ContactPerson = "Ayesha Siddiqui", Phone = "0312-3456789", Email = "orders@engrofoods.com", Address = "The Harbour Front, Clifton, Karachi", TaxID = "NTN-ENG-00013", IsDeleted = false, CreatedAt = now },
                new Company { CompanyName = "Haleeb Foods Ltd", ContactPerson = "Tariq Mehmood", Phone = "0313-4567890", Email = "supply@haleebfoods.com", Address = "Bhai Pheru, Kasur District, Punjab", TaxID = "NTN-HLB-00014", IsDeleted = false, CreatedAt = now },
                new Company { CompanyName = "Nurpur Dairy Pakistan", ContactPerson = "Rizwan Qureshi", Phone = "0314-5678901", Email = "orders@nurpurdairy.com", Address = "Sahiwal, Punjab", TaxID = "NTN-NUR-00015", IsDeleted = false, CreatedAt = now },
                new Company { CompanyName = "Good Milk (Dairy Omung)", ContactPerson = "Faisal Rehman", Phone = "0315-6789012", Email = "sales@goodmilk.pk", Address = "Plot 7, Sector 23, Korangi, Karachi", TaxID = "NTN-GMO-00016", IsDeleted = false, CreatedAt = now },

                // ===== JAMS, SAUCES & CONDIMENTS =====
                new Company { CompanyName = "Mitchell's Fruit Farms Ltd", ContactPerson = "Khalid Butt", Phone = "0316-7890123", Email = "info@mitchells.com.pk", Address = "Renala Khurd, Okara District, Punjab", TaxID = "NTN-MFF-00017", IsDeleted = false, CreatedAt = now },
                new Company { CompanyName = "Shangrila Foods (Pvt) Ltd", ContactPerson = "Junaid Iqbal", Phone = "0317-8901234", Email = "sales@shangrilafoods.com", Address = "Hattar Industrial Estate, Haripur, KPK", TaxID = "NTN-SGL-00018", IsDeleted = false, CreatedAt = now },
                new Company { CompanyName = "Dipitt Foods Pakistan", ContactPerson = "Ali Hassan", Phone = "0318-9012345", Email = "orders@dipitt.pk", Address = "I-9 Industrial Area, Islamabad", TaxID = "NTN-DIP-00019", IsDeleted = false, CreatedAt = now },

                // ===== COOKING OIL & GHEE =====
                new Company { CompanyName = "Dalda Foods (Pvt) Ltd", ContactPerson = "Shahid Mehmood", Phone = "0319-0123456", Email = "supply@dalda.com.pk", Address = "S.I.T.E. Area, Hyderabad, Sindh", TaxID = "NTN-DAL-00020", IsDeleted = false, CreatedAt = now },
                new Company { CompanyName = "Sufi Group of Companies", ContactPerson = "Waqas Anwar", Phone = "0320-1234567", Email = "orders@sufigroup.com", Address = "GT Road, Sahiwal, Punjab", TaxID = "NTN-SUF-00021", IsDeleted = false, CreatedAt = now },
                new Company { CompanyName = "Habib Oil Mills (Pvt) Ltd", ContactPerson = "Naveed Iqbal", Phone = "0321-2345678", Email = "sales@habiboil.com.pk", Address = "Port Qasim Industrial Zone, Karachi", TaxID = "NTN-HOM-00022", IsDeleted = false, CreatedAt = now },
                new Company { CompanyName = "Eva Fine Chemicals & Foods", ContactPerson = "Madiha Anwar", Phone = "0322-3456789", Email = "orders@evafoods.pk", Address = "Multan Road, Lahore", TaxID = "NTN-EVA-00023", IsDeleted = false, CreatedAt = now },

                // ===== RICE & GRAINS =====
                new Company { CompanyName = "Guard Rice Mills Pakistan", ContactPerson = "Abid Hussain", Phone = "0323-4567890", Email = "sales@guardrice.com", Address = "Kala Shah Kaku, Sheikhupura, Punjab", TaxID = "NTN-GRD-00024", IsDeleted = false, CreatedAt = now },
                new Company { CompanyName = "Matco Rice Processing Ltd", ContactPerson = "Shoaib Aslam", Phone = "0324-5678901", Email = "export@matcorice.com", Address = "S.I.T.E. Area, Karachi", TaxID = "NTN-MRC-00025", IsDeleted = false, CreatedAt = now },
                new Company { CompanyName = "Falak Rice Mills", ContactPerson = "Rehan Siddiqui", Phone = "0325-6789012", Email = "orders@falakrice.com", Address = "GT Road, Muridke, Punjab", TaxID = "NTN-FLK-00026", IsDeleted = false, CreatedAt = now },

                // ===== HOUSEHOLD & CLEANING =====
                new Company { CompanyName = "Lemon Max Home Care (Pvt) Ltd", ContactPerson = "Tahir Nawaz", Phone = "0326-7890123", Email = "supply@lemonmax.pk", Address = "West Wharf, Karachi", TaxID = "NTN-LMX-00027", IsDeleted = false, CreatedAt = now },
                new Company { CompanyName = "Bonus Detergent (Colgate-Palmolive)", ContactPerson = "Arif Khan", Phone = "0327-8901234", Email = "trade@bonus.pk", Address = "Hub Chowki Road, Hub, Balochistan", TaxID = "NTN-BNS-00028", IsDeleted = false, CreatedAt = now },

                // ===== BISCUITS & CONFECTIONERY =====
                new Company { CompanyName = "English Biscuit Manufacturers (EBM)", ContactPerson = "Adeel Zafar", Phone = "0330-1234567", Email = "orders@ebm.com.pk", Address = "S.I.T.E. Area, Super Highway, Karachi", TaxID = "NTN-EBM-00029", IsDeleted = false, CreatedAt = now },
                new Company { CompanyName = "Lu Biscuits (Continental Biscuits)", ContactPerson = "Sana Fatima", Phone = "0331-2345678", Email = "sales@lubiscuits.pk", Address = "Sukkur, Sindh", TaxID = "NTN-LUB-00030", IsDeleted = false, CreatedAt = now },
                new Company { CompanyName = "Hilal Foods (Pvt) Ltd", ContactPerson = "Kashif Raza", Phone = "0332-3456789", Email = "supply@hilalfoods.com", Address = "Korangi Industrial Area, Karachi", TaxID = "NTN-HIL-00031", IsDeleted = false, CreatedAt = now },
                new Company { CompanyName = "Candyland Industries (Ismail)", ContactPerson = "Yousuf Ali", Phone = "0333-4567890", Email = "orders@candyland.com.pk", Address = "M-3 Industrial City, Faisalabad", TaxID = "NTN-CDL-00032", IsDeleted = false, CreatedAt = now },

                // ===== PHARMACEUTICALS / HEALTH (often supply hygiene products) =====
                new Company { CompanyName = "Getz Pharma (Pvt) Ltd", ContactPerson = "Dr. Saleem Ahmad", Phone = "0334-5678901", Email = "supply@getzpharma.com", Address = "29-30, S.I.T.E., Karachi", TaxID = "NTN-GTZ-00033", IsDeleted = false, CreatedAt = now },
                new Company { CompanyName = "Martin Dow Group of Companies", ContactPerson = "Irfan Sheikh", Phone = "0335-6789012", Email = "orders@martindow.com", Address = "Quaid-e-Azam Industrial Estate, Lahore", TaxID = "NTN-MDG-00034", IsDeleted = false, CreatedAt = now },

                // ===== NOODLES & INSTANT FOOD =====
                new Company { CompanyName = "Knorr Foods (Unilever Subsidiary)", ContactPerson = "Amir Butt", Phone = "0336-7890123", Email = "knorr.supply@unilever.pk", Address = "Rahim Yar Khan, Punjab", TaxID = "NTN-KNR-00035", IsDeleted = false, CreatedAt = now },
                new Company { CompanyName = "Indomie Noodles Pakistan", ContactPerson = "Hassan Rauf", Phone = "0337-8901234", Email = "orders@indomie.pk", Address = "FIEDMC, M-3 Industrial City, Faisalabad", TaxID = "NTN-IND-00036", IsDeleted = false, CreatedAt = now },

                // ===== PAPER & TISSUE =====
                new Company { CompanyName = "Rose Petal (Packages Ltd)", ContactPerson = "Shahzad Amjad", Phone = "0340-1234567", Email = "trade@rosepetal.com.pk", Address = "Shahrah-e-Roomi, Lahore", TaxID = "NTN-RPL-00037", IsDeleted = false, CreatedAt = now },
                new Company { CompanyName = "Butterfly Tissue (Packages Ltd)", ContactPerson = "Waseem Ahmed", Phone = "0341-2345678", Email = "supply@butterfly.pk", Address = "Shahrah-e-Roomi, Lahore", TaxID = "NTN-BTF-00038", IsDeleted = false, CreatedAt = now },

                // ===== FROZEN FOODS =====
                new Company { CompanyName = "K&N's Food Pakistan (Pvt) Ltd", ContactPerson = "Owais Shah", Phone = "0342-3456789", Email = "wholesale@kns.com.pk", Address = "S.I.T.E. Area, Karachi", TaxID = "NTN-KNS-00039", IsDeleted = false, CreatedAt = now },
                new Company { CompanyName = "Mon Salwa Frozen Foods", ContactPerson = "Hamza Akbar", Phone = "0343-4567890", Email = "orders@monsalwa.pk", Address = "Manga Mandi, Lahore", TaxID = "NTN-MSF-00040", IsDeleted = false, CreatedAt = now }
            };

            await context.Companies.AddRangeAsync(companies);
            await context.SaveChangesAsync();

            return companies.ToDictionary(c => c.CompanyName, c => c);
        }

        private static async Task SeedProductsAndUnitsAsync(
            ApplicationDbContext context,
            int adminUserId,
            Dictionary<(int CompanyId, string Name), Category> categories,
            Dictionary<string, Unit> units,
            Dictionary<string, Company> companies)
        {
            if (await context.Products.IgnoreQueryFilters().AnyAsync()) return;

            var now = DateTime.UtcNow;

            // Definition data structure for seeding
            var seedProductDefs = new[]
            {
                // Coca-Cola Products
                new { Name="Coke 250ml Can", SKU="COKE-CAN-250", Barcode="890123400001", CatName="Beverages", BaseUnitName="Can", SellPrice=0.75m, Cost=0.50m, Reorder=100,
                      Units = new[] {
                          new { UnitName="Can", Conv=1m, BuyPrice=(decimal?)0.50m, SellPrice=(decimal?)0.75m, DefBuy=false, DefSell=true },
                          new { UnitName="Carton", Conv=24m, BuyPrice=(decimal?)12.00m, SellPrice=(decimal?)17.50m, DefBuy=true, DefSell=false }
                      }
                },
                new { Name="Coke 500ml Pet Bottle", SKU="COKE-BOT-500", Barcode="890123400002", CatName="Beverages", BaseUnitName="Bottle", SellPrice=1.20m, Cost=0.80m, Reorder=50,
                      Units = new[] {
                          new { UnitName="Bottle", Conv=1m, BuyPrice=(decimal?)0.80m, SellPrice=(decimal?)1.20m, DefBuy=false, DefSell=true },
                          new { UnitName="Carton", Conv=12m, BuyPrice=(decimal?)9.60m, SellPrice=(decimal?)14.00m, DefBuy=true, DefSell=false }
                      }
                },
                new { Name="Coke 1.5L Pet Bottle", SKU="COKE-BOT-1500", Barcode="890123400003", CatName="Beverages", BaseUnitName="Bottle", SellPrice=2.10m, Cost=1.40m, Reorder=40,
                      Units = new[] {
                          new { UnitName="Bottle", Conv=1m, BuyPrice=(decimal?)1.40m, SellPrice=(decimal?)2.10m, DefBuy=false, DefSell=true },
                          new { UnitName="Carton", Conv=6m, BuyPrice=(decimal?)8.40m, SellPrice=(decimal?)12.00m, DefBuy=true, DefSell=false }
                      }
                },
                new { Name="Sprite 250ml Can", SKU="SPR-CAN-250", Barcode="890123400004", CatName="Beverages", BaseUnitName="Can", SellPrice=0.75m, Cost=0.50m, Reorder=80,
                      Units = new[] {
                          new { UnitName="Can", Conv=1m, BuyPrice=(decimal?)0.50m, SellPrice=(decimal?)0.75m, DefBuy=false, DefSell=true },
                          new { UnitName="Carton", Conv=24m, BuyPrice=(decimal?)12.00m, SellPrice=(decimal?)17.50m, DefBuy=true, DefSell=false }
                      }
                },
                new { Name="Sprite 1.5L Pet Bottle", SKU="SPR-BOT-1500", Barcode="890123400005", CatName="Beverages", BaseUnitName="Bottle", SellPrice=2.10m, Cost=1.40m, Reorder=30,
                      Units = new[] {
                          new { UnitName="Bottle", Conv=1m, BuyPrice=(decimal?)1.40m, SellPrice=(decimal?)2.10m, DefBuy=false, DefSell=true },
                          new { UnitName="Carton", Conv=6m, BuyPrice=(decimal?)8.40m, SellPrice=(decimal?)12.00m, DefBuy=true, DefSell=false }
                      }
                },
                new { Name="Fanta Orange 250ml Can", SKU="FAN-CAN-250", Barcode="890123400006", CatName="Beverages", BaseUnitName="Can", SellPrice=0.75m, Cost=0.50m, Reorder=60,
                      Units = new[] {
                          new { UnitName="Can", Conv=1m, BuyPrice=(decimal?)0.50m, SellPrice=(decimal?)0.75m, DefBuy=false, DefSell=true },
                          new { UnitName="Carton", Conv=24m, BuyPrice=(decimal?)12.00m, SellPrice=(decimal?)17.50m, DefBuy=true, DefSell=false }
                      }
                },

                // PepsiCo Products
                new { Name="Pepsi 250ml Can", SKU="PEP-CAN-250", Barcode="890123400007", CatName="Beverages", BaseUnitName="Can", SellPrice=0.70m, Cost=0.48m, Reorder=100,
                      Units = new[] {
                          new { UnitName="Can", Conv=1m, BuyPrice=(decimal?)0.48m, SellPrice=(decimal?)0.70m, DefBuy=false, DefSell=true },
                          new { UnitName="Carton", Conv=24m, BuyPrice=(decimal?)11.50m, SellPrice=(decimal?)16.50m, DefBuy=true, DefSell=false }
                      }
                },
                new { Name="Pepsi 1.5L Bottle", SKU="PEP-BOT-1500", Barcode="890123400008", CatName="Beverages", BaseUnitName="Bottle", SellPrice=2.00m, Cost=1.35m, Reorder=40,
                      Units = new[] {
                          new { UnitName="Bottle", Conv=1m, BuyPrice=(decimal?)1.35m, SellPrice=(decimal?)2.00m, DefBuy=false, DefSell=true },
                          new { UnitName="Carton", Conv=6m, BuyPrice=(decimal?)8.10m, SellPrice=(decimal?)11.50m, DefBuy=true, DefSell=false }
                      }
                },
                new { Name="Mountain Dew 500ml Bottle", SKU="DEW-BOT-500", Barcode="890123400009", CatName="Beverages", BaseUnitName="Bottle", SellPrice=1.15m, Cost=0.78m, Reorder=50,
                      Units = new[] {
                          new { UnitName="Bottle", Conv=1m, BuyPrice=(decimal?)0.78m, SellPrice=(decimal?)1.15m, DefBuy=false, DefSell=true },
                          new { UnitName="Carton", Conv=12m, BuyPrice=(decimal?)9.36m, SellPrice=(decimal?)13.50m, DefBuy=true, DefSell=false }
                      }
                },
                new { Name="7Up 1.5L Bottle", SKU="7UP-BOT-1500", Barcode="890123400010", CatName="Beverages", BaseUnitName="Bottle", SellPrice=2.00m, Cost=1.35m, Reorder=30,
                      Units = new[] {
                          new { UnitName="Bottle", Conv=1m, BuyPrice=(decimal?)1.35m, SellPrice=(decimal?)2.00m, DefBuy=false, DefSell=true },
                          new { UnitName="Carton", Conv=6m, BuyPrice=(decimal?)8.10m, SellPrice=(decimal?)11.50m, DefBuy=true, DefSell=false }
                      }
                },
                new { Name="Lay's Classic Salted 40g", SKU="LAYS-SALT-40", Barcode="890123400011", CatName="Snacks & Confectionery", BaseUnitName="Packet", SellPrice=0.60m, Cost=0.40m, Reorder=120,
                      Units = new[] {
                          new { UnitName="Packet", Conv=1m, BuyPrice=(decimal?)0.40m, SellPrice=(decimal?)0.60m, DefBuy=false, DefSell=true },
                          new { UnitName="Box", Conv=24m, BuyPrice=(decimal?)9.60m, SellPrice=(decimal?)14.00m, DefBuy=true, DefSell=false }
                      }
                },
                new { Name="Lay's Masala Wavy 40g", SKU="LAYS-MASALA-40", Barcode="890123400012", CatName="Snacks & Confectionery", BaseUnitName="Packet", SellPrice=0.60m, Cost=0.40m, Reorder=100,
                      Units = new[] {
                          new { UnitName="Packet", Conv=1m, BuyPrice=(decimal?)0.40m, SellPrice=(decimal?)0.60m, DefBuy=false, DefSell=true },
                          new { UnitName="Box", Conv=24m, BuyPrice=(decimal?)9.60m, SellPrice=(decimal?)14.00m, DefBuy=true, DefSell=false }
                      }
                },
                new { Name="Cheetos Cheese Puffs 30g", SKU="CHEE-CHEESE-30", Barcode="890123400013", CatName="Snacks & Confectionery", BaseUnitName="Packet", SellPrice=0.50m, Cost=0.35m, Reorder=80,
                      Units = new[] {
                          new { UnitName="Packet", Conv=1m, BuyPrice=(decimal?)0.35m, SellPrice=(decimal?)0.50m, DefBuy=false, DefSell=true },
                          new { UnitName="Box", Conv=30m, BuyPrice=(decimal?)10.50m, SellPrice=(decimal?)14.50m, DefBuy=true, DefSell=false }
                      }
                },

                // Nestlé Products
                new { Name="Nestlé Pure Life Water 500ml", SKU="NES-WAT-500", Barcode="890123400014", CatName="Beverages", BaseUnitName="Bottle", SellPrice=0.40m, Cost=0.25m, Reorder=150,
                      Units = new[] {
                          new { UnitName="Bottle", Conv=1m, BuyPrice=(decimal?)0.25m, SellPrice=(decimal?)0.40m, DefBuy=false, DefSell=true },
                          new { UnitName="Carton", Conv=24m, BuyPrice=(decimal?)6.00m, SellPrice=(decimal?)9.00m, DefBuy=true, DefSell=false }
                      }
                },
                new { Name="Nestlé Pure Life Water 1.5L", SKU="NES-WAT-1500", Barcode="890123400015", CatName="Beverages", BaseUnitName="Bottle", SellPrice=0.80m, Cost=0.50m, Reorder=100,
                      Units = new[] {
                          new { UnitName="Bottle", Conv=1m, BuyPrice=(decimal?)0.50m, SellPrice=(decimal?)0.80m, DefBuy=false, DefSell=true },
                          new { UnitName="Carton", Conv=6m, BuyPrice=(decimal?)3.00m, SellPrice=(decimal?)4.50m, DefBuy=true, DefSell=false }
                      }
                },
                new { Name="Nestlé MilkPak 1L UHT", SKU="MILKPAK-1L", Barcode="890123400016", CatName="Dairy & Milk Products", BaseUnitName="Packet", SellPrice=1.80m, Cost=1.30m, Reorder=80,
                      Units = new[] {
                          new { UnitName="Packet", Conv=1m, BuyPrice=(decimal?)1.30m, SellPrice=(decimal?)1.80m, DefBuy=false, DefSell=true },
                          new { UnitName="Carton", Conv=12m, BuyPrice=(decimal?)15.60m, SellPrice=(decimal?)21.00m, DefBuy=true, DefSell=false }
                      }
                },
                new { Name="Nestlé Everyday Tea Whitener 400g", SKU="EVERYDAY-400", Barcode="890123400017", CatName="Dairy & Milk Products", BaseUnitName="Packet", SellPrice=3.50m, Cost=2.60m, Reorder=40,
                      Units = new[] {
                          new { UnitName="Packet", Conv=1m, BuyPrice=(decimal?)2.60m, SellPrice=(decimal?)3.50m, DefBuy=false, DefSell=true },
                          new { UnitName="Carton", Conv=12m, BuyPrice=(decimal?)31.20m, SellPrice=(decimal?)41.00m, DefBuy=true, DefSell=false }
                      }
                },
                new { Name="Nescafé Classic 3-in-1 Stick Box", SKU="NESCAFE-3IN1", Barcode="890123400018", CatName="Tea & Coffee", BaseUnitName="Packet", SellPrice=0.30m, Cost=0.20m, Reorder=200,
                      Units = new[] {
                          new { UnitName="Packet", Conv=1m, BuyPrice=(decimal?)0.20m, SellPrice=(decimal?)0.30m, DefBuy=false, DefSell=true },
                          new { UnitName="Box", Conv=30m, BuyPrice=(decimal?)6.00m, SellPrice=(decimal?)8.50m, DefBuy=true, DefSell=false }
                      }
                },

                // Unilever Products
                new { Name="Lux Beauty Soap Velvet Touch 140g", SKU="LUX-VELVET-140", Barcode="890123400019", CatName="Personal Care & Hygiene", BaseUnitName="Piece", SellPrice=0.90m, Cost=0.65m, Reorder=100,
                      Units = new[] {
                          new { UnitName="Piece", Conv=1m, BuyPrice=(decimal?)0.65m, SellPrice=(decimal?)0.90m, DefBuy=false, DefSell=true },
                          new { UnitName="Box", Conv=12m, BuyPrice=(decimal?)7.80m, SellPrice=(decimal?)10.50m, DefBuy=true, DefSell=false }
                      }
                },
                new { Name="Lifebuoy Total 10 Soap 135g", SKU="LIFE-TOTAL-135", Barcode="890123400020", CatName="Personal Care & Hygiene", BaseUnitName="Piece", SellPrice=0.80m, Cost=0.58m, Reorder=100,
                      Units = new[] {
                          new { UnitName="Piece", Conv=1m, BuyPrice=(decimal?)0.58m, SellPrice=(decimal?)0.80m, DefBuy=false, DefSell=true },
                          new { UnitName="Box", Conv=12m, BuyPrice=(decimal?)6.96m, SellPrice=(decimal?)9.20m, DefBuy=true, DefSell=false }
                      }
                },
                new { Name="Sunsilk Black Shine Shampoo 180ml", SKU="SUN-BLACK-180", Barcode="890123400021", CatName="Personal Care & Hygiene", BaseUnitName="Bottle", SellPrice=2.50m, Cost=1.80m, Reorder=40,
                      Units = new[] {
                          new { UnitName="Bottle", Conv=1m, BuyPrice=(decimal?)1.80m, SellPrice=(decimal?)2.50m, DefBuy=false, DefSell=true },
                          new { UnitName="Carton", Conv=12m, BuyPrice=(decimal?)21.60m, SellPrice=(decimal?)29.00m, DefBuy=true, DefSell=false }
                      }
                },
                new { Name="Surf Excel Washing Powder 1KG", SKU="SURF-EXCEL-1KG", Barcode="890123400022", CatName="Household & Cleaning", BaseUnitName="Packet", SellPrice=4.20m, Cost=3.10m, Reorder=50,
                      Units = new[] {
                          new { UnitName="Packet", Conv=1m, BuyPrice=(decimal?)3.10m, SellPrice=(decimal?)4.20m, DefBuy=false, DefSell=true },
                          new { UnitName="Carton", Conv=10m, BuyPrice=(decimal?)31.00m, SellPrice=(decimal?)40.00m, DefBuy=true, DefSell=false }
                      }
                },
                new { Name="Vim Dishwash Bar 300g", SKU="VIM-BAR-300", Barcode="890123400023", CatName="Household & Cleaning", BaseUnitName="Piece", SellPrice=0.95m, Cost=0.70m, Reorder=60,
                      Units = new[] {
                          new { UnitName="Piece", Conv=1m, BuyPrice=(decimal?)0.70m, SellPrice=(decimal?)0.95m, DefBuy=false, DefSell=true },
                          new { UnitName="Box", Conv=24m, BuyPrice=(decimal?)16.80m, SellPrice=(decimal?)22.00m, DefBuy=true, DefSell=false }
                      }
                },
                new { Name="Knorr Chicken Cubes 12s Pack", SKU="KNORR-CUBES-12", Barcode="890123400024", CatName="Spices & Recipe Mixes", BaseUnitName="Box", SellPrice=1.50m, Cost=1.10m, Reorder=40,
                      Units = new[] {
                          new { UnitName="Box", Conv=1m, BuyPrice=(decimal?)1.10m, SellPrice=(decimal?)1.50m, DefBuy=false, DefSell=true },
                          new { UnitName="Carton", Conv=24m, BuyPrice=(decimal?)26.40m, SellPrice=(decimal?)34.00m, DefBuy=true, DefSell=false }
                      }
                },

                // National Foods
                new { Name="National Bombay Biryani Masala 50g", SKU="NAT-BIRYANI-50", Barcode="890123400025", CatName="Spices & Recipe Mixes", BaseUnitName="Packet", SellPrice=0.85m, Cost=0.60m, Reorder=100,
                      Units = new[] {
                          new { UnitName="Packet", Conv=1m, BuyPrice=(decimal?)0.60m, SellPrice=(decimal?)0.85m, DefBuy=false, DefSell=true },
                          new { UnitName="Box", Conv=12m, BuyPrice=(decimal?)7.20m, SellPrice=(decimal?)9.80m, DefBuy=true, DefSell=false }
                      }
                },
                new { Name="National Korma Masala 50g", SKU="NAT-KORMA-50", Barcode="890123400026", CatName="Spices & Recipe Mixes", BaseUnitName="Packet", SellPrice=0.85m, Cost=0.60m, Reorder=80,
                      Units = new[] {
                          new { UnitName="Packet", Conv=1m, BuyPrice=(decimal?)0.60m, SellPrice=(decimal?)0.85m, DefBuy=false, DefSell=true },
                          new { UnitName="Box", Conv=12m, BuyPrice=(decimal?)7.20m, SellPrice=(decimal?)9.80m, DefBuy=true, DefSell=false }
                      }
                },
                new { Name="National Tomato Ketchup 500g Pouch", SKU="NAT-KETCHUP-500", Barcode="890123400027", CatName="Jams, Sauces & Condiments", BaseUnitName="Packet", SellPrice=1.90m, Cost=1.35m, Reorder=50,
                      Units = new[] {
                          new { UnitName="Packet", Conv=1m, BuyPrice=(decimal?)1.35m, SellPrice=(decimal?)1.90m, DefBuy=false, DefSell=true },
                          new { UnitName="Carton", Conv=12m, BuyPrice=(decimal?)16.20m, SellPrice=(decimal?)22.00m, DefBuy=true, DefSell=false }
                      }
                },
                new { Name="National Chilli Garlic Sauce 500g", SKU="NAT-CG-500", Barcode="890123400028", CatName="Jams, Sauces & Condiments", BaseUnitName="Bottle", SellPrice=2.10m, Cost=1.50m, Reorder=40,
                      Units = new[] {
                          new { UnitName="Bottle", Conv=1m, BuyPrice=(decimal?)1.50m, SellPrice=(decimal?)2.10m, DefBuy=false, DefSell=true },
                          new { UnitName="Carton", Conv=12m, BuyPrice=(decimal?)18.00m, SellPrice=(decimal?)24.50m, DefBuy=true, DefSell=false }
                      }
                },
                new { Name="National Red Chilli Powder 200g", SKU="NAT-REDCHILLI-200", Barcode="890123400029", CatName="Spices & Recipe Mixes", BaseUnitName="Packet", SellPrice=1.60m, Cost=1.15m, Reorder=60,
                      Units = new[] {
                          new { UnitName="Packet", Conv=1m, BuyPrice=(decimal?)1.15m, SellPrice=(decimal?)1.60m, DefBuy=false, DefSell=true },
                          new { UnitName="Box", Conv=12m, BuyPrice=(decimal?)13.80m, SellPrice=(decimal?)18.50m, DefBuy=true, DefSell=false }
                      }
                },

                // Shan Foods
                new { Name="Shan Special Sindhi Biryani Masala 50g", SKU="SHAN-SINDHI-50", Barcode="890123400030", CatName="Spices & Recipe Mixes", BaseUnitName="Packet", SellPrice=0.90m, Cost=0.62m, Reorder=100,
                      Units = new[] {
                          new { UnitName="Packet", Conv=1m, BuyPrice=(decimal?)0.62m, SellPrice=(decimal?)0.90m, DefBuy=false, DefSell=true },
                          new { UnitName="Box", Conv=12m, BuyPrice=(decimal?)7.44m, SellPrice=(decimal?)10.20m, DefBuy=true, DefSell=false }
                      }
                },
                new { Name="Shan Haleem Mix 300g", SKU="SHAN-HALEEM-300", Barcode="890123400031", CatName="Spices & Recipe Mixes", BaseUnitName="Packet", SellPrice=1.75m, Cost=1.25m, Reorder=50,
                      Units = new[] {
                          new { UnitName="Packet", Conv=1m, BuyPrice=(decimal?)1.25m, SellPrice=(decimal?)1.75m, DefBuy=false, DefSell=true },
                          new { UnitName="Box", Conv=12m, BuyPrice=(decimal?)15.00m, SellPrice=(decimal?)20.00m, DefBuy=true, DefSell=false }
                      }
                },
                new { Name="Shan Nihari Masala 60g", SKU="SHAN-NIHARI-60", Barcode="890123400032", CatName="Spices & Recipe Mixes", BaseUnitName="Packet", SellPrice=0.90m, Cost=0.62m, Reorder=60,
                      Units = new[] {
                          new { UnitName="Packet", Conv=1m, BuyPrice=(decimal?)0.62m, SellPrice=(decimal?)0.90m, DefBuy=false, DefSell=true },
                          new { UnitName="Box", Conv=12m, BuyPrice=(decimal?)7.44m, SellPrice=(decimal?)10.20m, DefBuy=true, DefSell=false }
                      }
                },
                new { Name="Shan Shoop Instant Noodles Masala 70g", SKU="SHAN-SHOOP-70", Barcode="890123400033", CatName="Snacks & Confectionery", BaseUnitName="Packet", SellPrice=0.40m, Cost=0.28m, Reorder=120,
                      Units = new[] {
                          new { UnitName="Packet", Conv=1m, BuyPrice=(decimal?)0.28m, SellPrice=(decimal?)0.40m, DefBuy=false, DefSell=true },
                          new { UnitName="Box", Conv=24m, BuyPrice=(decimal?)6.72m, SellPrice=(decimal?)9.00m, DefBuy=true, DefSell=false }
                      }
                },

                // Tapal Tea
                new { Name="Tapal Danedar Black Tea 450g", SKU="TAPAL-DANE-450", Barcode="890123400034", CatName="Tea & Coffee", BaseUnitName="Packet", SellPrice=4.50m, Cost=3.40m, Reorder=50,
                      Units = new[] {
                          new { UnitName="Packet", Conv=1m, BuyPrice=(decimal?)3.40m, SellPrice=(decimal?)4.50m, DefBuy=false, DefSell=true },
                          new { UnitName="Carton", Conv=12m, BuyPrice=(decimal?)40.80m, SellPrice=(decimal?)52.00m, DefBuy=true, DefSell=false }
                      }
                },
                new { Name="Tapal Family Mixture Tea 900g", SKU="TAPAL-FAM-900", Barcode="890123400035", CatName="Tea & Coffee", BaseUnitName="Packet", SellPrice=8.20m, Cost=6.20m, Reorder=30,
                      Units = new[] {
                          new { UnitName="Packet", Conv=1m, BuyPrice=(decimal?)6.20m, SellPrice=(decimal?)8.20m, DefBuy=false, DefSell=true },
                          new { UnitName="Carton", Conv=6m, BuyPrice=(decimal?)37.20m, SellPrice=(decimal?)47.50m, DefBuy=true, DefSell=false }
                      }
                },
                new { Name="Tapal Green Tea Jasmine 30 Teabags", SKU="TAPAL-GREEN-30", Barcode="890123400036", CatName="Tea & Coffee", BaseUnitName="Box", SellPrice=2.20m, Cost=1.60m, Reorder=40,
                      Units = new[] {
                          new { UnitName="Box", Conv=1m, BuyPrice=(decimal?)1.60m, SellPrice=(decimal?)2.20m, DefBuy=false, DefSell=true },
                          new { UnitName="Carton", Conv=12m, BuyPrice=(decimal?)19.20m, SellPrice=(decimal?)25.00m, DefBuy=true, DefSell=false }
                      }
                },

                // Mitchell's Fruit Farms
                new { Name="Mitchell's Mixed Fruit Jam 450g Jar", SKU="MITCH-JAM-450", Barcode="890123400037", CatName="Jams, Sauces & Condiments", BaseUnitName="Bottle", SellPrice=2.80m, Cost=2.00m, Reorder=40,
                      Units = new[] {
                          new { UnitName="Bottle", Conv=1m, BuyPrice=(decimal?)2.00m, SellPrice=(decimal?)2.80m, DefBuy=false, DefSell=true },
                          new { UnitName="Carton", Conv=12m, BuyPrice=(decimal?)24.00m, SellPrice=(decimal?)32.00m, DefBuy=true, DefSell=false }
                      }
                },
                new { Name="Mitchell's Mango Squash 800ml", SKU="MITCH-SQUASH-800", Barcode="890123400038", CatName="Beverages", BaseUnitName="Bottle", SellPrice=3.20m, Cost=2.30m, Reorder=30,
                      Units = new[] {
                          new { UnitName="Bottle", Conv=1m, BuyPrice=(decimal?)2.30m, SellPrice=(decimal?)3.20m, DefBuy=false, DefSell=true },
                          new { UnitName="Carton", Conv=12m, BuyPrice=(decimal?)27.60m, SellPrice=(decimal?)36.50m, DefBuy=true, DefSell=false }
                      }
                },
                new { Name="Mitchell's Tomato Paste 400g Can", SKU="MITCH-PASTE-400", Barcode="890123400039", CatName="Jams, Sauces & Condiments", BaseUnitName="Can", SellPrice=1.60m, Cost=1.15m, Reorder=50,
                      Units = new[] {
                          new { UnitName="Can", Conv=1m, BuyPrice=(decimal?)1.15m, SellPrice=(decimal?)1.60m, DefBuy=false, DefSell=true },
                          new { UnitName="Carton", Conv=24m, BuyPrice=(decimal?)27.60m, SellPrice=(decimal?)36.00m, DefBuy=true, DefSell=false }
                      }
                },

                // Engro Foods (Olper's)
                new { Name="Olper's Full Cream Milk 1L UHT", SKU="OLPERS-MILK-1L", Barcode="890123400040", CatName="Dairy & Milk Products", BaseUnitName="Packet", SellPrice=1.75m, Cost=1.28m, Reorder=100,
                      Units = new[] {
                          new { UnitName="Packet", Conv=1m, BuyPrice=(decimal?)1.28m, SellPrice=(decimal?)1.75m, DefBuy=false, DefSell=true },
                          new { UnitName="Carton", Conv=12m, BuyPrice=(decimal?)15.36m, SellPrice=(decimal?)20.50m, DefBuy=true, DefSell=false }
                      }
                },
                new { Name="Olper's Cream 200ml", SKU="OLPERS-CREAM-200", Barcode="890123400041", CatName="Dairy & Milk Products", BaseUnitName="Packet", SellPrice=1.10m, Cost=0.80m, Reorder=60,
                      Units = new[] {
                          new { UnitName="Packet", Conv=1m, BuyPrice=(decimal?)0.80m, SellPrice=(decimal?)1.10m, DefBuy=false, DefSell=true },
                          new { UnitName="Carton", Conv=24m, BuyPrice=(decimal?)19.20m, SellPrice=(decimal?)25.00m, DefBuy=true, DefSell=false }
                      }
                },
                new { Name="Olper's Flavoured Milk Chocolate 180ml", SKU="OLPERS-CHOC-180", Barcode="890123400042", CatName="Dairy & Milk Products", BaseUnitName="Packet", SellPrice=0.65m, Cost=0.45m, Reorder=80,
                      Units = new[] {
                          new { UnitName="Packet", Conv=1m, BuyPrice=(decimal?)0.45m, SellPrice=(decimal?)0.65m, DefBuy=false, DefSell=true },
                          new { UnitName="Carton", Conv=24m, BuyPrice=(decimal?)10.80m, SellPrice=(decimal?)15.00m, DefBuy=true, DefSell=false }
                      }
                },

                // Procter & Gamble
                new { Name="Head & Shoulders Shampoo 180ml", SKU="HS-SHAMP-180", Barcode="890123400043", CatName="Personal Care & Hygiene", BaseUnitName="Bottle", SellPrice=3.10m, Cost=2.30m, Reorder=40,
                      Units = new[] {
                          new { UnitName="Bottle", Conv=1m, BuyPrice=(decimal?)2.30m, SellPrice=(decimal?)3.10m, DefBuy=false, DefSell=true },
                          new { UnitName="Carton", Conv=12m, BuyPrice=(decimal?)27.60m, SellPrice=(decimal?)35.50m, DefBuy=true, DefSell=false }
                      }
                },
                new { Name="Pampers Baby Dry Medium 36s", SKU="PAMPERS-M-36", Barcode="890123400044", CatName="Personal Care & Hygiene", BaseUnitName="Bag", SellPrice=12.50m, Cost=9.50m, Reorder=25,
                      Units = new[] {
                          new { UnitName="Bag", Conv=1m, BuyPrice=(decimal?)9.50m, SellPrice=(decimal?)12.50m, DefBuy=true, DefSell=true }
                      }
                },
                new { Name="Ariel Original Detergent Powder 1KG", SKU="ARIEL-1KG", Barcode="890123400045", CatName="Household & Cleaning", BaseUnitName="Packet", SellPrice=4.50m, Cost=3.30m, Reorder=50,
                      Units = new[] {
                          new { UnitName="Packet", Conv=1m, BuyPrice=(decimal?)3.30m, SellPrice=(decimal?)4.50m, DefBuy=false, DefSell=true },
                          new { UnitName="Carton", Conv=10m, BuyPrice=(decimal?)33.00m, SellPrice=(decimal?)43.00m, DefBuy=true, DefSell=false }
                      }
                },
                new { Name="Gillette Blue II Plus Razors 5s", SKU="GILLETTE-BLUE-5", Barcode="890123400046", CatName="Personal Care & Hygiene", BaseUnitName="Packet", SellPrice=2.80m, Cost=2.00m, Reorder=40,
                      Units = new[] {
                          new { UnitName="Packet", Conv=1m, BuyPrice=(decimal?)2.00m, SellPrice=(decimal?)2.80m, DefBuy=false, DefSell=true },
                          new { UnitName="Box", Conv=20m, BuyPrice=(decimal?)40.00m, SellPrice=(decimal?)53.00m, DefBuy=true, DefSell=false }
                      }
                },

                // Rice & Oils
                new { Name="Super Basmati Premium Rice 5KG Bag", SKU="RICE-BAS-5KG", Barcode="890123400047", CatName="Rice & Grains", BaseUnitName="Bag", SellPrice=9.50m, Cost=7.20m, Reorder=30,
                      Units = new[] {
                          new { UnitName="Bag", Conv=1m, BuyPrice=(decimal?)7.20m, SellPrice=(decimal?)9.50m, DefBuy=true, DefSell=true }
                      }
                },
                new { Name="Super Basmati Premium Rice 20KG Bag", SKU="RICE-BAS-20KG", Barcode="890123400048", CatName="Rice & Grains", BaseUnitName="Bag", SellPrice=36.00m, Cost=27.00m, Reorder=20,
                      Units = new[] {
                          new { UnitName="Bag", Conv=1m, BuyPrice=(decimal?)27.00m, SellPrice=(decimal?)36.00m, DefBuy=true, DefSell=true }
                      }
                },
                new { Name="Dalda Cooking Oil 1L Pouch", SKU="DALDA-OIL-1L", Barcode="890123400049", CatName="Cooking Oil & Ghee", BaseUnitName="Packet", SellPrice=3.20m, Cost=2.40m, Reorder=60,
                      Units = new[] {
                          new { UnitName="Packet", Conv=1m, BuyPrice=(decimal?)2.40m, SellPrice=(decimal?)3.20m, DefBuy=false, DefSell=true },
                          new { UnitName="Carton", Conv=12m, BuyPrice=(decimal?)28.80m, SellPrice=(decimal?)37.00m, DefBuy=true, DefSell=false }
                      }
                },
                new { Name="Dalda Banaspati Ghee 1KG Tin", SKU="DALDA-GHEE-1KG", Barcode="890123400050", CatName="Cooking Oil & Ghee", BaseUnitName="Can", SellPrice=3.50m, Cost=2.60m, Reorder=50,
                      Units = new[] {
                          new { UnitName="Can", Conv=1m, BuyPrice=(decimal?)2.60m, SellPrice=(decimal?)3.50m, DefBuy=false, DefSell=true },
                          new { UnitName="Carton", Conv=12m, BuyPrice=(decimal?)31.20m, SellPrice=(decimal?)40.50m, DefBuy=true, DefSell=false }
                      }
                }
            };

            int prodIdx = 0;
            var companiesList = companies.Values.ToList();

            foreach (var pDef in seedProductDefs)
            {
                var baseUnit = units[pDef.BaseUnitName];
                var company = companiesList[prodIdx % companiesList.Count];
                prodIdx++;

                if (!categories.TryGetValue((company.CompanyID, pDef.CatName), out var cat))
                {
                    throw new InvalidOperationException(
                        $"Seed category '{pDef.CatName}' was not found for company '{company.CompanyName}'.");
                }

                var product = new Product
                {
                    ProductName = pDef.Name,
                    SKU = pDef.SKU,
                    Barcode = pDef.Barcode,
                    CategoryID = cat.CategoryID,
                    CompanyID = company.CompanyID,
                    BaseUnitID = baseUnit.UnitID,
                    BaseSellingPrice = pDef.SellPrice,
                    AveragePurchaseCost = pDef.Cost,
                    ReorderLevel = pDef.Reorder,
                    IsActive = true,
                    CreatedAt = now,
                    CreatedBy = adminUserId,
                    IsDeleted = false
                };

                await context.Products.AddAsync(product);
                await context.SaveChangesAsync(); // Auto-generates ProductID in SQL Server

                foreach (var uDef in pDef.Units)
                {
                    var unit = units[uDef.UnitName];

                    var productUnit = new ProductUnit
                    {
                        ProductID = product.ProductID,
                        UnitID = unit.UnitID,
                        ConversionToBaseUnit = uDef.Conv,
                        PurchasePrice = uDef.BuyPrice,
                        SellingPrice = uDef.SellPrice,
                        IsDefaultPurchaseUnit = uDef.DefBuy,
                        IsDefaultSalesUnit = uDef.DefSell,
                        IsActive = true,
                        CreatedAt = now,
                        CreatedBy = adminUserId,
                        IsDeleted = false
                    };

                    await context.ProductUnits.AddAsync(productUnit);
                }

                await context.SaveChangesAsync();
            }
        }

        private static async Task<(Dictionary<string, Area> Areas, Dictionary<string, SubArea> SubAreas)> SeedAreasAndSubAreasAsync(ApplicationDbContext context)
        {
            var existingAreas = await context.Areas.IgnoreQueryFilters().Include(a => a.SubAreas).ToListAsync();
            if (existingAreas.Any())
            {
                var existingSubAreas = existingAreas.SelectMany(a => a.SubAreas).ToDictionary(sa => sa.SubAreaName, sa => sa);
                return (existingAreas.ToDictionary(a => a.AreaName, a => a), existingSubAreas);
            }

            var now = DateTime.UtcNow;

            var areaDefs = new[]
            {
                new { Name = "Karachi Central", Code = "KHI-CEN", SubAreas = new[] { "Gulberg", "North Nazimabad", "Federal B Area", "Buffer Zone" } },
                new { Name = "Karachi South", Code = "KHI-STH", SubAreas = new[] { "Clifton", "Saddar", "Defense (DHA)", "Tariq Road" } },
                new { Name = "Lahore Gulberg", Code = "LHR-GLB", SubAreas = new[] { "Main Market", "Liberty Market", "MM Alam Road", "Model Town" } },
                new { Name = "Lahore Johar Town", Code = "LHR-JHR", SubAreas = new[] { "Doctor Hospital Chowk", "G1 Market", "Wapda Town", "Cavalry Ground" } },
                new { Name = "Islamabad I-8", Code = "ISB-I8", SubAreas = new[] { "I-8 Markaz", "F-6 Markaz", "F-7 Markaz", "G-9 Markaz" } },
                new { Name = "Rawalpindi Saddar", Code = "RWP-SDR", SubAreas = new[] { "Bank Road", "Commercial Market", "Satellite Town", "Raja Bazar" } },
                new { Name = "Faisalabad Clock Tower", Code = "FSD-CT", SubAreas = new[] { "D-Ground", "Batala Colony", "Peoples Colony", "Gulberg FSD" } }
            };

            var areasDict = new Dictionary<string, Area>();
            var subAreasDict = new Dictionary<string, SubArea>();

            foreach (var def in areaDefs)
            {
                var area = new Area
                {
                    AreaName = def.Name,
                    Code = def.Code,
                    IsActive = true,
                    IsDeleted = false,
                    CreatedAt = now
                };

                await context.Areas.AddAsync(area);
                await context.SaveChangesAsync();
                areasDict[area.AreaName] = area;

                foreach (var saName in def.SubAreas)
                {
                    var subArea = new SubArea
                    {
                        AreaID = area.AreaID,
                        SubAreaName = saName,
                        Code = $"{def.Code}-{saName.Substring(0, Math.Min(3, saName.Length)).ToUpper()}",
                        IsActive = true,
                        IsDeleted = false,
                        CreatedAt = now
                    };

                    await context.SubAreas.AddAsync(subArea);
                    await context.SaveChangesAsync();
                    subAreasDict[subArea.SubAreaName] = subArea;
                }
            }

            return (areasDict, subAreasDict);
        }

        private static async Task SeedCustomersAsync(
            ApplicationDbContext context,
            int adminUserId,
            Dictionary<string, Area> areas,
            Dictionary<string, SubArea> subAreas)
        {
            if (await context.Customers.IgnoreQueryFilters().AnyAsync()) return;

            var now = DateTime.UtcNow;

            var customerDefs = new[]
            {
                // ===== KARACHI CENTRAL =====
                new { Shop = "Al-Madina Super Store", Owner = "Muhammad Tariq", Phone = "0300-9876543", Area = "Karachi Central", SubArea = "Gulberg", Address = "Shop 12, Main Chowk, Gulberg, Karachi", Tax = "NTN-CUST-0001", Credit = 150000m },
                new { Shop = "Subhan General Store", Owner = "Subhan Ali", Phone = "0301-8765432", Area = "Karachi Central", SubArea = "North Nazimabad", Address = "Block H, North Nazimabad, Karachi", Tax = "NTN-CUST-0002", Credit = 100000m },
                new { Shop = "Bismillah Traders", Owner = "Haji Abdul Rehman", Phone = "0302-7654321", Area = "Karachi Central", SubArea = "Federal B Area", Address = "Block 14, FB Area, Karachi", Tax = "NTN-CUST-0003", Credit = 200000m },
                new { Shop = "Raza Cash & Carry", Owner = "Ahmed Raza", Phone = "0303-6543210", Area = "Karachi Central", SubArea = "Buffer Zone", Address = "Sector 15-A, Buffer Zone, Karachi", Tax = "NTN-CUST-0004", Credit = 80000m },
                new { Shop = "Kashif Departmental Store", Owner = "Kashif Hussain", Phone = "0304-5432109", Area = "Karachi Central", SubArea = "Gulberg", Address = "Shop 45, Water Pump Market, Karachi", Tax = "NTN-CUST-0005", Credit = 120000m },
                new { Shop = "Nafees Bakers & Mart", Owner = "Nafees Ahmed", Phone = "0305-4321098", Area = "Karachi Central", SubArea = "North Nazimabad", Address = "Block L, North Nazimabad, Karachi", Tax = "NTN-CUST-0006", Credit = 250000m },

                // ===== KARACHI SOUTH =====
                new { Shop = "Clifton Mart & Grocery", Owner = "Shahid Khan", Phone = "0306-3210987", Area = "Karachi South", SubArea = "Clifton", Address = "Block 5, Kehkashan, Clifton, Karachi", Tax = "NTN-CUST-0007", Credit = 300000m },
                new { Shop = "Saddar Wholesale Emporium", Owner = "Imran Qureshi", Phone = "0307-2109876", Area = "Karachi South", SubArea = "Saddar", Address = "Preedy Street, Saddar, Karachi", Tax = "NTN-CUST-0008", Credit = 500000m },
                new { Shop = "Defense Supermarket", Owner = "Bilal Sheikh", Phone = "0308-1098765", Area = "Karachi South", SubArea = "Defense (DHA)", Address = "Phase 5, Commercial Area, DHA, Karachi", Tax = "NTN-CUST-0009", Credit = 400000m },
                new { Shop = "Tariq Road General Mart", Owner = "Usman Farooq", Phone = "0309-0987654", Area = "Karachi South", SubArea = "Tariq Road", Address = "Main Tariq Road, Karachi", Tax = "NTN-CUST-0010", Credit = 180000m },
                new { Shop = "Avari Mini Mart", Owner = "Farhan Zafar", Phone = "0310-9876543", Area = "Karachi South", SubArea = "Saddar", Address = "Near Avari Towers, Saddar, Karachi", Tax = "NTN-CUST-0011", Credit = 150000m },
                new { Shop = "Ocean Mall Express Store", Owner = "Asif Iqbal", Phone = "0311-8765432", Area = "Karachi South", SubArea = "Clifton", Address = "Khayaban-e-Iqbal, Clifton, Karachi", Tax = "NTN-CUST-0012", Credit = 350000m },

                // ===== LAHORE GULBERG =====
                new { Shop = "Al-Fatah Express Gulberg", Owner = "Shafiq Ahmed", Phone = "0312-7654321", Area = "Lahore Gulberg", SubArea = "Main Market", Address = "Main Market, Gulberg II, Lahore", Tax = "NTN-CUST-0013", Credit = 600000m },
                new { Shop = "Liberty Cash & Carry", Owner = "Kamran Butt", Phone = "0313-6543210", Area = "Lahore Gulberg", SubArea = "Liberty Market", Address = "Liberty Roundabout, Gulberg III, Lahore", Tax = "NTN-CUST-0014", Credit = 450000m },
                new { Shop = "MM Alam Gourmet Store", Owner = "Faisal Malik", Phone = "0314-5432109", Area = "Lahore Gulberg", SubArea = "MM Alam Road", Address = "MM Alam Road, Gulberg III, Lahore", Tax = "NTN-CUST-0015", Credit = 500000m },
                new { Shop = "Model Town General Store", Owner = "Zahid Hussain", Phone = "0315-4321098", Area = "Lahore Gulberg", SubArea = "Model Town", Address = "C-Block Market, Model Town, Lahore", Tax = "NTN-CUST-0016", Credit = 200000m },
                new { Shop = "Gourmet Bakers & Mart", Owner = "Zubair Latif", Phone = "0316-3210987", Area = "Lahore Gulberg", SubArea = "Main Market", Address = "Main Market, Gulberg, Lahore", Tax = "NTN-CUST-0017", Credit = 300000m },
                new { Shop = "Hafeez Center Corner Store", Owner = "Nasir Mehmood", Phone = "0317-2109876", Area = "Lahore Gulberg", SubArea = "Main Market", Address = "Hafeez Center Plaza, Gulberg, Lahore", Tax = "NTN-CUST-0018", Credit = 100000m },

                // ===== LAHORE JOHAR TOWN =====
                new { Shop = "Doctor Hospital Super Store", Owner = "Rana Tanveer", Phone = "0318-1098765", Area = "Lahore Johar Town", SubArea = "Doctor Hospital Chowk", Address = "Near Doctor Hospital, Johar Town, Lahore", Tax = "NTN-CUST-0019", Credit = 220000m },
                new { Shop = "G1 Market Mart", Owner = "Khalid Mansoor", Phone = "0319-0987654", Area = "Lahore Johar Town", SubArea = "G1 Market", Address = "G1 Market, Johar Town, Lahore", Tax = "NTN-CUST-0020", Credit = 180000m },
                new { Shop = "Wapda Town Cash & Carry", Owner = "Asad Chaudhry", Phone = "0320-9876543", Area = "Lahore Johar Town", SubArea = "Wapda Town", Address = "Roundabout 2, Wapda Town, Lahore", Tax = "NTN-CUST-0021", Credit = 250000m },
                new { Shop = "Cavalry Grocery Corner", Owner = "Waseem Akram", Phone = "0321-8765432", Area = "Lahore Johar Town", SubArea = "Cavalry Ground", Address = "Commercial Extension, Cavalry Ground, Lahore", Tax = "NTN-CUST-0022", Credit = 350000m },
                new { Shop = "Khayaban-e-Firdousi Mart", Owner = "Shoaib Akhtar", Phone = "0322-7654321", Area = "Lahore Johar Town", SubArea = "G1 Market", Address = "Firdousi Avenue, Johar Town, Lahore", Tax = "NTN-CUST-0023", Credit = 140000m },
                new { Shop = "PNSC Super Store", Owner = "Noman Khalid", Phone = "0323-6543210", Area = "Lahore Johar Town", SubArea = "Doctor Hospital Chowk", Address = "Phase 2, Johar Town, Lahore", Tax = "NTN-CUST-0024", Credit = 160000m },

                // ===== ISLAMABAD I-8 =====
                new { Shop = "I-8 Markaz Cash & Carry", Owner = "Tariq Aziz", Phone = "0324-5432109", Area = "Islamabad I-8", SubArea = "I-8 Markaz", Address = "Plot 12, I-8 Markaz, Islamabad", Tax = "NTN-CUST-0025", Credit = 400000m },
                new { Shop = "Kohsar Supermarket (F-6)", Owner = "Sikandar Hayat", Phone = "0325-4321098", Area = "Islamabad I-8", SubArea = "F-6 Markaz", Address = "Kohsar Market, F-6/3, Islamabad", Tax = "NTN-CUST-0026", Credit = 550000m },
                new { Shop = "Jinnah Super Store (F-7)", Owner = "Hamza Abbasi", Phone = "0326-3210987", Area = "Islamabad I-8", SubArea = "F-7 Markaz", Address = "Jinnah Super Market, F-7, Islamabad", Tax = "NTN-CUST-0027", Credit = 500000m },
                new { Shop = "Karachi Company Grocery (G-9)", Owner = "Shakeel Khan", Phone = "0327-2109876", Area = "Islamabad I-8", SubArea = "G-9 Markaz", Address = "G-9 Markaz (Karachi Company), Islamabad", Tax = "NTN-CUST-0028", Credit = 250000m },
                new { Shop = "Save Mart I-8", Owner = "Rashid Minhas", Phone = "0328-1098765", Area = "Islamabad I-8", SubArea = "I-8 Markaz", Address = "Executive Plaza, I-8 Markaz, Islamabad", Tax = "NTN-CUST-0029", Credit = 300000m },
                new { Shop = "Islamabad Traders F-7", Owner = "Javed Iqbal", Phone = "0329-0987654", Area = "Islamabad I-8", SubArea = "F-7 Markaz", Address = "Shop 8, F-7 Markaz, Islamabad", Tax = "NTN-CUST-0030", Credit = 200000m },

                // ===== RAWALPINDI SADDAR =====
                new { Shop = "Bank Road General Store", Owner = "Iftikhar Ahmed", Phone = "0330-9876543", Area = "Rawalpindi Saddar", SubArea = "Bank Road", Address = "Bank Road, Cantt, Rawalpindi", Tax = "NTN-CUST-0031", Credit = 280000m },
                new { Shop = "Commercial Market Mart", Owner = "Amjad Ali", Phone = "0331-8765432", Area = "Rawalpindi Saddar", SubArea = "Commercial Market", Address = "Commercial Market, Satellite Town, Rawalpindi", Tax = "NTN-CUST-0032", Credit = 350000m },
                new { Shop = "Satellite Town Grocery", Owner = "Zafar Iqbal", Phone = "0332-7654321", Area = "Rawalpindi Saddar", SubArea = "Satellite Town", Address = "Block B, Satellite Town, Rawalpindi", Tax = "NTN-CUST-0033", Credit = 150000m },
                new { Shop = "Raja Bazar Traders", Owner = "Seth Ghani", Phone = "0333-6543210", Area = "Rawalpindi Saddar", SubArea = "Raja Bazar", Address = "Ganj Mandi, Raja Bazar, Rawalpindi", Tax = "NTN-CUST-0034", Credit = 600000m },
                new { Shop = "Cantt Cash & Carry", Owner = "Major (Rtd) Younas", Phone = "0334-5432109", Area = "Rawalpindi Saddar", SubArea = "Bank Road", Address = "Haider Road, Saddar, Rawalpindi", Tax = "NTN-CUST-0035", Credit = 400000m },
                new { Shop = "Murree Road Super Store", Owner = "Adnan Saeed", Phone = "0335-4321098", Area = "Rawalpindi Saddar", SubArea = "Commercial Market", Address = "Murree Road Chowk, Rawalpindi", Tax = "NTN-CUST-0036", Credit = 200000m },

                // ===== FAISALABAD CLOCK TOWER =====
                new { Shop = "D-Ground Mega Mart", Owner = "Chaudhry Bashir", Phone = "0336-3210987", Area = "Faisalabad Clock Tower", SubArea = "D-Ground", Address = "D-Ground Commercial Zone, Faisalabad", Tax = "NTN-CUST-0037", Credit = 450000m },
                new { Shop = "Batala Colony Cash & Carry", Owner = "Mian Mansha", Phone = "0337-2109876", Area = "Faisalabad Clock Tower", SubArea = "Batala Colony", Address = "Main Boulevard, Batala Colony, Faisalabad", Tax = "NTN-CUST-0038", Credit = 300000m },
                new { Shop = "Peoples Colony General Store", Owner = "Hafiz Tanveer", Phone = "0338-1098765", Area = "Faisalabad Clock Tower", SubArea = "Peoples Colony", Address = "Saleemi Chowk, Peoples Colony 1, Faisalabad", Tax = "NTN-CUST-0039", Credit = 220000m },
                new { Shop = "Clock Tower Wholesale Bazar", Owner = "Seth Mushtaq", Phone = "0339-0987654", Area = "Faisalabad Clock Tower", SubArea = "Gulberg FSD", Address = "Karkhana Bazar, Clock Tower, Faisalabad", Tax = "NTN-CUST-0040", Credit = 500000m },
                new { Shop = "Chenab Super Mart", Owner = "Rizwan Tariq", Phone = "0340-9876543", Area = "Faisalabad Clock Tower", SubArea = "D-Ground", Address = "Chenab Club Road, Faisalabad", Tax = "NTN-CUST-0041", Credit = 350000m },
                new { Shop = "Samanabad Traders", Owner = "Haroon Rashid", Phone = "0341-8765432", Area = "Faisalabad Clock Tower", SubArea = "Peoples Colony", Address = "Main Road, Samanabad, Faisalabad", Tax = "NTN-CUST-0042", Credit = 180000m }
            };

            var customerEntities = new List<Customer>();

            foreach (var cDef in customerDefs)
            {
                var area = areas.ContainsKey(cDef.Area) ? areas[cDef.Area] : null;
                var subArea = subAreas.ContainsKey(cDef.SubArea) ? subAreas[cDef.SubArea] : null;

                var customer = new Customer
                {
                    ShopName = cDef.Shop,
                    OwnerName = cDef.Owner,
                    Phone = cDef.Phone,
                    Address = cDef.Address,
                    AreaID = area?.AreaID,
                    SubAreaID = subArea?.SubAreaID,
                    TaxID = cDef.Tax,
                    CreditLimit = cDef.Credit,
                    IsActive = true,
                    CreatedAt = now,
                    CreatedBy = adminUserId,
                    IsDeleted = false
                };

                customerEntities.Add(customer);
            }

            await context.Customers.AddRangeAsync(customerEntities);
            await context.SaveChangesAsync();
        }

        private static async Task<List<Warehouse>> SeedWarehousesAsync(ApplicationDbContext context, int adminUserId)
        {
            var existing = await context.Warehouses.IgnoreQueryFilters().ToListAsync();
            if (existing.Any())
            {
                return existing;
            }

            var warehouses = new List<Warehouse>
            {
                new Warehouse { Name = "Main Warehouse", Address = "Plot 45, SITE Industrial Area, Karachi", IsActive = true, IsMain = true, IsDeleted = false, CreatedAt = DateTime.UtcNow, CreatedBy = adminUserId },
                new Warehouse { Name = "North Warehouse", Address = "Sector I-9/3 Industrial Area, Islamabad", IsActive = true, IsMain = false, IsDeleted = false, CreatedAt = DateTime.UtcNow, CreatedBy = adminUserId },
                new Warehouse { Name = "South Warehouse", Address = "Sunder Industrial Estate, Lahore", IsActive = true, IsMain = false, IsDeleted = false, CreatedAt = DateTime.UtcNow, CreatedBy = adminUserId }
            };

            await context.Warehouses.AddRangeAsync(warehouses);
            await context.SaveChangesAsync();
            return warehouses;
        }

        private static async Task SeedPurchaseInvoicesAsync(ApplicationDbContext context, int adminUserId, List<Warehouse> warehouses)
        {
            if (await context.PurchaseInvoices.IgnoreQueryFilters().AnyAsync())
            {
                return;
            }

            var companies = await context.Companies.Where(c => !c.IsDeleted).Take(5).ToListAsync();
            var products = await context.Products.Include(p => p.ProductUnits).ThenInclude(pu => pu.Unit).Where(p => !p.IsDeleted && p.IsActive).Take(10).ToListAsync();

            if (!companies.Any() || !products.Any() || !warehouses.Any())
            {
                return;
            }

            var mainWarehouse = warehouses.First();
            var now = DateTime.UtcNow;

            for (int i = 1; i <= 6; i++)
            {
                var company = companies[(i - 1) % companies.Count];
                var product1 = products[(i * 2 - 2) % products.Count];
                var product2 = products[(i * 2 - 1) % products.Count];

                var pu1 = product1.ProductUnits.FirstOrDefault(u => u.IsDefaultPurchaseUnit) ?? product1.ProductUnits.First();
                var pu2 = product2.ProductUnits.FirstOrDefault(u => u.IsDefaultPurchaseUnit) ?? product2.ProductUnits.First();

                decimal qty1 = 10 + i * 2;
                decimal cost1 = 500 + i * 50;
                decimal convertedQty1 = qty1 * pu1.ConversionToBaseUnit;
                decimal totalCost1 = qty1 * cost1;

                decimal qty2 = 5 + i;
                decimal cost2 = 800 + i * 100;
                decimal convertedQty2 = qty2 * pu2.ConversionToBaseUnit;
                decimal totalCost2 = qty2 * cost2;

                decimal grandTotal = totalCost1 + totalCost2;
                string invoiceNo = $"PINV-2026080{i}-00{i}";

                var invoice = new PurchaseInvoice
                {
                    CompanyID = company.CompanyID,
                    WarehouseID = mainWarehouse.WarehouseID,
                    InvoiceNumber = invoiceNo,
                    InvoiceDate = DateTime.Today.AddDays(-15 + i * 2),
                    SubTotal = grandTotal,
                    DiscountAmount = 0m,
                    TaxAmount = 0m,
                    GrandTotal = grandTotal,
                    PaidAmount = 0m,
                    PaymentStatus = "UNPAID",
                    CreatedBy = adminUserId,
                    CreatedAt = now,
                    IsDeleted = false
                };

                await context.PurchaseInvoices.AddAsync(invoice);
                await context.SaveChangesAsync();

                var item1 = new PurchaseInvoiceItem
                {
                    PurchaseInvoiceID = invoice.PurchaseInvoiceID,
                    ProductID = product1.ProductID,
                    ProductUnitID = pu1.ProductUnitID,
                    Quantity = qty1,
                    ConvertedQuantity = convertedQty1,
                    UnitCost = cost1,
                    TotalCost = totalCost1,
                    IsActive = true,
                    CreatedAt = now,
                    CreatedBy = adminUserId,
                    IsDeleted = false
                };

                var item2 = new PurchaseInvoiceItem
                {
                    PurchaseInvoiceID = invoice.PurchaseInvoiceID,
                    ProductID = product2.ProductID,
                    ProductUnitID = pu2.ProductUnitID,
                    Quantity = qty2,
                    ConvertedQuantity = convertedQty2,
                    UnitCost = cost2,
                    TotalCost = totalCost2,
                    IsActive = true,
                    CreatedAt = now,
                    CreatedBy = adminUserId,
                    IsDeleted = false
                };

                await context.PurchaseInvoiceItems.AddRangeAsync(item1, item2);
                await context.SaveChangesAsync();

                // Update InventoryStock
                var stock1 = await context.InventoryStocks.FirstOrDefaultAsync(s => s.ProductID == product1.ProductID && s.WarehouseID == mainWarehouse.WarehouseID && !s.IsDeleted);
                if (stock1 == null)
                {
                    stock1 = new InventoryStock { ProductID = product1.ProductID, WarehouseID = mainWarehouse.WarehouseID, Quantity = convertedQty1, IsActive = true, IsDeleted = false, CreatedAt = now, CreatedBy = adminUserId };
                    await context.InventoryStocks.AddAsync(stock1);
                }
                else
                {
                    stock1.Quantity += convertedQty1;
                }

                var stock2 = await context.InventoryStocks.FirstOrDefaultAsync(s => s.ProductID == product2.ProductID && s.WarehouseID == mainWarehouse.WarehouseID && !s.IsDeleted);
                if (stock2 == null)
                {
                    stock2 = new InventoryStock { ProductID = product2.ProductID, WarehouseID = mainWarehouse.WarehouseID, Quantity = convertedQty2, IsActive = true, IsDeleted = false, CreatedAt = now, CreatedBy = adminUserId };
                    await context.InventoryStocks.AddAsync(stock2);
                }
                else
                {
                    stock2.Quantity += convertedQty2;
                }

                // Inventory Transactions
                var tx1 = new InventoryTransaction { ProductID = product1.ProductID, WarehouseID = mainWarehouse.WarehouseID, TransactionType = "PURCHASE", Quantity = convertedQty1, ReferenceNumber = invoiceNo, PurchaseInvoiceItemID = item1.PurchaseItemID, CreatedBy = adminUserId, CreatedAt = now, IsActive = true, IsDeleted = false };
                var tx2 = new InventoryTransaction { ProductID = product2.ProductID, WarehouseID = mainWarehouse.WarehouseID, TransactionType = "PURCHASE", Quantity = convertedQty2, ReferenceNumber = invoiceNo, PurchaseInvoiceItemID = item2.PurchaseItemID, CreatedBy = adminUserId, CreatedAt = now, IsActive = true, IsDeleted = false };
                await context.InventoryTransactions.AddRangeAsync(tx1, tx2);

                // Company Ledger
                var ledger = new CompanyLedger { CompanyID = company.CompanyID, TransactionDate = invoice.InvoiceDate, TransactionType = "PURCHASE", DebitAmount = 0m, CreditAmount = grandTotal, PurchaseInvoiceID = invoice.PurchaseInvoiceID, Description = $"Purchase Invoice #{invoiceNo}", CreatedBy = adminUserId, CreatedAt = now };
                await context.CompanyLedgers.AddAsync(ledger);

                await context.SaveChangesAsync();
            }
        }

        private static async Task<List<User>> SeedSalespersonUsersAsync(
            ApplicationDbContext context,
            int adminRoleId,
            int adminUserId)
        {
            var users = await context.Users
                .IgnoreQueryFilters()
                .Where(u => !u.IsDeleted && u.IsActive)
                .OrderBy(u => u.UserID)
                .ToListAsync();

            if (users.Count >= 2)
            {
                return users;
            }

            var existing = await context.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Username == "shahzaib");

            if (existing == null)
            {
                existing = new User
                {
                    RoleID = adminRoleId,
                    FullName = "Shahzaib",
                    Username = "shahzaib",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                    IsActive = true,
                    IsDeleted = false,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = adminUserId
                };

                await context.Users.AddAsync(existing);
                await context.SaveChangesAsync();
            }

            users = await context.Users
                .IgnoreQueryFilters()
                .Where(u => !u.IsDeleted && u.IsActive)
                .OrderBy(u => u.UserID)
                .ToListAsync();

            return users;
        }

        private static async Task BackfillExistingInvoicesForLoadSheetTestingAsync(
            ApplicationDbContext context,
            List<Booker> bookers,
            List<User> salespersons,
            List<Supplier> suppliers)
        {
            if (bookers.Count < 2 || !salespersons.Any())
            {
                return;
            }

            // A Booker belongs to exactly one Company (BR-043), so only invoices of that same
            // company are eligible for booker backfill.
            int bookerCompanyId = bookers[0].CompanyID;

            var invoices = await context.SalesInvoices
                .IgnoreQueryFilters()
                .Where(si => !si.IsDeleted)
                .Where(si => si.CompanyID == bookerCompanyId)
                .OrderBy(si => si.InvoiceDate)
                .ThenBy(si => si.InvoiceID)
                .ToListAsync();

            if (!invoices.Any())
            {
                return;
            }

            // Only Booker, Salesperson and Supplier are backfilled. The invoice company is already
            // persisted on each invoice, so no company is derived here.
            bool bookersAlreadyAssigned = invoices.Any(i => i.BookerID.HasValue);
            if (!bookersAlreadyAssigned)
            {
                var bookerA = bookers[0];
                var bookerB = bookers[1];
                var bookerC = bookers.Count > 2 ? bookers[2] : bookerA;
                var userA = salespersons[0];
                var userB = salespersons.Count > 1 ? salespersons[1] : salespersons[0];

                var dateGroups = invoices
                    .GroupBy(i => i.InvoiceDate.Date)
                    .OrderBy(g => g.Key)
                    .ToList();

                bool assignedBookerC = false;

                foreach (var group in dateGroups)
                {
                    var list = group.OrderBy(i => i.InvoiceID).ToList();
                    int splitIndex;
                    if (list.Count >= 4)
                    {
                        splitIndex = list.Count / 2;
                    }
                    else if (list.Count == 3)
                    {
                        splitIndex = 2;
                    }
                    else if (list.Count == 2)
                    {
                        splitIndex = 1;
                    }
                    else
                    {
                        splitIndex = list.Count;
                    }

                    for (int i = 0; i < list.Count; i++)
                    {
                        var invoice = list[i];
                        bool assignBookerC = list.Count == 1 && !assignedBookerC;

                        if (assignBookerC)
                        {
                            invoice.BookerID = bookerC.BookerID;
                            invoice.SalespersonID = userA.UserID;
                            assignedBookerC = true;
                            continue;
                        }

                        bool inBookerAGroup = i < splitIndex;
                        invoice.BookerID = inBookerAGroup ? bookerA.BookerID : bookerB.BookerID;

                        if (inBookerAGroup && list.Count >= 3 && i < 2)
                        {
                            invoice.SalespersonID = userB.UserID;
                        }
                        else if (!inBookerAGroup)
                        {
                            invoice.SalespersonID = userB.UserID;
                        }
                        else
                        {
                            invoice.SalespersonID = userA.UserID;
                        }
                    }
                }
            }

            if (suppliers.Any())
            {
                var unassigned = invoices.Where(i => !i.SupplierID.HasValue).ToList();
                for (int i = 0; i < unassigned.Count; i++)
                {
                    unassigned[i].SupplierID = suppliers[i % suppliers.Count].SupplierID;
                }
            }

            await context.SaveChangesAsync();
        }

        private static async Task<List<Booker>> SeedBookersAsync(
            ApplicationDbContext context,
            Dictionary<string, Company> companies)
        {
            if (await context.Bookers.IgnoreQueryFilters().AnyAsync())
            {
                return await context.Bookers.IgnoreQueryFilters().Where(b => !b.IsDeleted).ToListAsync();
            }

            // A Booker belongs to exactly one Company (BR-043). All seed bookers share the first
            // company so the seeded single-company sales invoices (BR-044) stay coherent.
            var company = companies.Values.OrderBy(c => c.CompanyID).FirstOrDefault();
            if (company == null)
            {
                return new List<Booker>();
            }

            var now = DateTime.UtcNow;

            var bookers = new List<Booker>
            {
                new Booker { CompanyID = company.CompanyID, Name = "Nadeem", CNIC = "42101-1000001-1", Phone = "03001234567", IsActive = true, IsDeleted = false, CreatedAt = now },
                new Booker { CompanyID = company.CompanyID, Name = "Imran", CNIC = "42101-1000002-1", Phone = "03007654321", IsActive = true, IsDeleted = false, CreatedAt = now },
                new Booker { CompanyID = company.CompanyID, Name = "Asif", CNIC = "42101-1000003-1", Phone = "03009876543", IsActive = true, IsDeleted = false, CreatedAt = now }
            };

            await context.Bookers.AddRangeAsync(bookers);
            await context.SaveChangesAsync();
            return bookers;
        }

        private static async Task<List<Supplier>> SeedSuppliersAsync(ApplicationDbContext context)
        {
            if (await context.Suppliers.IgnoreQueryFilters().AnyAsync())
            {
                return await context.Suppliers.IgnoreQueryFilters().Where(dp => !dp.IsDeleted).ToListAsync();
            }

            var suppliers = new List<Supplier>
            {
                new Supplier { Name = "Khalid", CNIC = "42101-0000001-1", Phone = "03001112233", Address = null, Type = "Employee", IsActive = true, IsDeleted = false, CreatedAt = DateTime.UtcNow },
                new Supplier { Name = "Waseem", CNIC = "42101-0000002-1", Phone = "03004445566", Address = null, Type = "Employee", IsActive = true, IsDeleted = false, CreatedAt = DateTime.UtcNow },
                new Supplier { Name = "Tariq", CNIC = "42101-0000003-1", Phone = "03007778899", Address = null, Type = "External", IsActive = true, IsDeleted = false, CreatedAt = DateTime.UtcNow }
            };

            await context.Suppliers.AddRangeAsync(suppliers);
            await context.SaveChangesAsync();
            return suppliers;
        }

        private static async Task SeedSalesInvoicesAsync(
            ApplicationDbContext context,
            int adminUserId,
            List<Warehouse> warehouses,
            List<Booker> bookers,
            List<Supplier> suppliers)
        {
            if (await context.SalesInvoices.IgnoreQueryFilters().AnyAsync()) return;

            var mainWarehouse = warehouses.FirstOrDefault(w => w.IsMain) ?? warehouses.First();
            var customers = await context.Customers.IgnoreQueryFilters().Where(c => !c.IsDeleted).ToListAsync();
            var products = await context.Products.IgnoreQueryFilters().Include(p => p.ProductUnits).Where(p => !p.IsDeleted).ToListAsync();

            if (!customers.Any() || !products.Any() || !bookers.Any()) return;

            // One invoice, one company (BR-044). The invoice company comes from the booker's company,
            // so every line item must be a product of that same company.
            int invoiceCompanyId = bookers[0].CompanyID;
            var companyProducts = products.Where(p => p.CompanyID == invoiceCompanyId).ToList();
            if (!companyProducts.Any()) return;

            var companyBookers = bookers.Where(b => b.CompanyID == invoiceCompanyId).ToList();
            if (!companyBookers.Any()) return;

            var now = DateTime.UtcNow;

            for (int i = 1; i <= 4; i++)
            {
                var customer = customers[(i - 1) % customers.Count];
                var product1 = companyProducts[(i - 1) % companyProducts.Count];
                var product2 = companyProducts.Count > 1
                    ? companyProducts[i % companyProducts.Count]
                    : product1;
                var booker = companyBookers[(i - 1) % companyBookers.Count];
                var supplier = suppliers.Any() ? suppliers[(i - 1) % suppliers.Count] : null;
                var invoiceDate = now.AddDays(-i);

                var pu1 = product1.ProductUnits.FirstOrDefault() ?? new ProductUnit { UnitID = product1.BaseUnitID, ConversionToBaseUnit = 1m };
                var pu2 = product2.ProductUnits.FirstOrDefault() ?? new ProductUnit { UnitID = product2.BaseUnitID, ConversionToBaseUnit = 1m };

                decimal qty1 = 5m;
                decimal qty2 = 2m;
                decimal price1 = product1.BaseSellingPrice > 0 ? product1.BaseSellingPrice : 15.00m;
                decimal price2 = product2.BaseSellingPrice > 0 ? product2.BaseSellingPrice : 25.00m;

                decimal totalCost1 = qty1 * price1;
                decimal totalCost2 = qty2 * price2;
                decimal grandTotal = totalCost1 + totalCost2;

                decimal convertedQty1 = qty1 * pu1.ConversionToBaseUnit;
                decimal convertedQty2 = qty2 * pu2.ConversionToBaseUnit;

                string invoiceNo = $"INV-{invoiceDate:yyyyMMdd}-000{i}";

                var invoice = new SalesInvoice
                {
                    CustomerID = customer.CustomerID,
                    CompanyID = invoiceCompanyId,
                    BookerID = booker.BookerID,
                    SalespersonID = adminUserId,
                    SupplierID = supplier?.SupplierID,
                    WarehouseID = mainWarehouse.WarehouseID,
                    AreaID = customer.AreaID,
                    SubAreaID = customer.SubAreaID,
                    InvoiceNumber = invoiceNo,
                    InvoiceDate = invoiceDate,
                    SubTotal = grandTotal,
                    DiscountTotal = 0m,
                    TaxTotal = 0m,
                    GrandTotal = grandTotal,
                    PaidAmount = 0m,
                    PaymentStatus = "UNPAID",
                    IsLocked = false,
                    CreatedAt = invoiceDate,
                    CreatedBy = adminUserId,
                    IsDeleted = false
                };

                await context.SalesInvoices.AddAsync(invoice);
                await context.SaveChangesAsync();

                var item1 = new SalesInvoiceItem
                {
                    InvoiceID = invoice.InvoiceID,
                    ProductID = product1.ProductID,
                    ProductUnitID = pu1.ProductUnitID,
                    Quantity = qty1,
                    ConvertedQuantity = convertedQty1,
                    UnitPrice = price1,
                    DiscountAmount = 0m,
                    ItemType = "NORMAL",
                    IsActive = true,
                    CreatedAt = invoiceDate,
                    CreatedBy = adminUserId,
                    IsDeleted = false
                };

                var item2 = new SalesInvoiceItem
                {
                    InvoiceID = invoice.InvoiceID,
                    ProductID = product2.ProductID,
                    ProductUnitID = pu2.ProductUnitID,
                    Quantity = qty2,
                    ConvertedQuantity = convertedQty2,
                    UnitPrice = price2,
                    DiscountAmount = 0m,
                    ItemType = "NORMAL",
                    IsActive = true,
                    CreatedAt = invoiceDate,
                    CreatedBy = adminUserId,
                    IsDeleted = false
                };

                await context.SalesInvoiceItems.AddRangeAsync(item1, item2);
                await context.SaveChangesAsync();

                // Deduct InventoryStock
                var stock1 = await context.InventoryStocks.FirstOrDefaultAsync(s => s.ProductID == product1.ProductID && s.WarehouseID == mainWarehouse.WarehouseID && !s.IsDeleted);
                if (stock1 != null)
                {
                    stock1.Quantity = Math.Max(0, stock1.Quantity - convertedQty1);
                }

                var stock2 = await context.InventoryStocks.FirstOrDefaultAsync(s => s.ProductID == product2.ProductID && s.WarehouseID == mainWarehouse.WarehouseID && !s.IsDeleted);
                if (stock2 != null)
                {
                    stock2.Quantity = Math.Max(0, stock2.Quantity - convertedQty2);
                }

                // Inventory Transactions (Outward Stock Movement)
                var tx1 = new InventoryTransaction { ProductID = product1.ProductID, WarehouseID = mainWarehouse.WarehouseID, TransactionType = "SALE", Quantity = -convertedQty1, ReferenceNumber = invoiceNo, SalesInvoiceItemID = item1.InvoiceItemID, CreatedBy = adminUserId, CreatedAt = invoiceDate, IsActive = true, IsDeleted = false };
                var tx2 = new InventoryTransaction { ProductID = product2.ProductID, WarehouseID = mainWarehouse.WarehouseID, TransactionType = "SALE", Quantity = -convertedQty2, ReferenceNumber = invoiceNo, SalesInvoiceItemID = item2.InvoiceItemID, CreatedBy = adminUserId, CreatedAt = invoiceDate, IsActive = true, IsDeleted = false };
                await context.InventoryTransactions.AddRangeAsync(tx1, tx2);

                // Customer Ledger (Debit Entry)
                var ledger = new CustomerLedger { CustomerID = customer.CustomerID, TransactionDate = invoice.InvoiceDate, TransactionType = "SALE", DebitAmount = grandTotal, CreditAmount = 0m, SalesInvoiceID = invoice.InvoiceID, Description = $"Sales Invoice #{invoiceNo}", CreatedBy = adminUserId, CreatedAt = invoiceDate };
                await context.CustomerLedgers.AddAsync(ledger);

                await context.SaveChangesAsync();
            }
        }

        private static async Task SeedPromotionsAndDiscountsAsync(ApplicationDbContext context, int adminUserId)
        {
            var now = DateTime.UtcNow;

            // Promotions and discount rules are company-scoped, and a promotion's buy/free products
            // must belong to that same company. Pick the first company that has at least 2 products.
            var allProducts = await context.Products.IgnoreQueryFilters().Where(p => !p.IsDeleted).ToListAsync();
            var scopedProducts = allProducts
                .GroupBy(p => p.CompanyID)
                .Where(g => g.Count() >= 2)
                .OrderBy(g => g.Key)
                .Select(g => g.OrderBy(p => p.ProductID).ToList())
                .FirstOrDefault();

            if (scopedProducts == null)
            {
                return;
            }

            int promoCompanyId = scopedProducts[0].CompanyID;

            // Seed Promotion Campaign if none exist
            if (!await context.PromotionCampaigns.IgnoreQueryFilters().AnyAsync())
            {
                var products = scopedProducts;
                if (products.Count >= 2)
                {
                    var p1 = products[0];
                    var p2 = products[1];

                    var campaign = new PromotionCampaign
                    {
                        Name = "Bulk Wholesale Buy 5 Get 1 Free Promo",
                        CompanyID = promoCompanyId,
                        StartDate = now.AddDays(-30),
                        EndDate = now.AddDays(180),
                        IsActive = true,
                        CreatedBy = adminUserId,
                        IsDeleted = false
                    };

                    await context.PromotionCampaigns.AddAsync(campaign);
                    await context.SaveChangesAsync();

                    var rule = new PromotionRule
                    {
                        PromotionID = campaign.PromotionID,
                        BuyProductID = p1.ProductID,
                        BuyQuantity = 5,
                        FreeProductID = p2.ProductID,
                        FreeQuantity = 1
                    };

                    await context.PromotionRules.AddAsync(rule);
                    await context.SaveChangesAsync();
                }
            }

            // Seed Order Discount Rules if none exist
            if (!await context.DiscountRules.IgnoreQueryFilters().AnyAsync())
            {
                var rule1 = new DiscountRule
                {
                    RuleName = "Bulk Order 5% Discount (Orders > PKR 5,000)",
                    CompanyID = promoCompanyId,
                    MinimumOrderAmount = 5000m,
                    DiscountType = "Percentage",
                    DiscountValue = 5m,
                    StartDate = now.AddDays(-30),
                    EndDate = now.AddDays(365),
                    IsActive = true,
                    Priority = 1,
                    CreatedBy = adminUserId,
                    CreatedAt = now,
                    IsDeleted = false
                };

                var rule2 = new DiscountRule
                {
                    RuleName = "VIP Order 10% Discount (Orders > PKR 15,000)",
                    CompanyID = promoCompanyId,
                    MinimumOrderAmount = 15000m,
                    DiscountType = "Percentage",
                    DiscountValue = 10m,
                    StartDate = now.AddDays(-30),
                    EndDate = now.AddDays(365),
                    IsActive = true,
                    Priority = 2,
                    CreatedBy = adminUserId,
                    CreatedAt = now,
                    IsDeleted = false
                };

                await context.DiscountRules.AddRangeAsync(rule1, rule2);
                await context.SaveChangesAsync();
            }
        }

        private static async Task SeedPermissionCatalogAsync(ApplicationDbContext context)
        {
            foreach (var moduleSeed in PermissionSeedData.GetModulesAndPages())
            {
                var module = await context.ApplicationModules
                    .FirstOrDefaultAsync(m => m.ModuleKey == moduleSeed.ModuleKey);

                if (module == null)
                {
                    module = new ApplicationModule
                    {
                        ModuleKey = moduleSeed.ModuleKey,
                        ModuleName = moduleSeed.ModuleName,
                        DisplayOrder = moduleSeed.Order,
                        IsActive = true
                    };
                    await context.ApplicationModules.AddAsync(module);
                    await context.SaveChangesAsync();
                }
                else
                {
                    module.ModuleName = moduleSeed.ModuleName;
                    module.DisplayOrder = moduleSeed.Order;
                    module.IsActive = true;
                }

                foreach (var pageSeed in moduleSeed.Pages)
                {
                    var page = await context.ApplicationPages
                        .FirstOrDefaultAsync(p => p.PageKey == pageSeed.PageKey);

                    if (page == null)
                    {
                        await context.ApplicationPages.AddAsync(new ApplicationPage
                        {
                            ApplicationModuleID = module.ApplicationModuleID,
                            PageKey = pageSeed.PageKey,
                            PageName = pageSeed.PageName,
                            ControllerName = pageSeed.Controller,
                            DefaultActionName = pageSeed.Action,
                            DisplayOrder = pageSeed.PageOrder,
                            IsActive = true
                        });
                    }
                    else
                    {
                        page.ApplicationModuleID = module.ApplicationModuleID;
                        page.PageName = pageSeed.PageName;
                        page.ControllerName = pageSeed.Controller;
                        page.DefaultActionName = pageSeed.Action;
                        page.DisplayOrder = pageSeed.PageOrder;
                        page.IsActive = true;
                    }
                }
            }

            await context.SaveChangesAsync();
        }

        private static async Task SeedDefaultUserRolePermissionsAsync(ApplicationDbContext context, int userRoleId)
        {
            if (await context.RolePermissions.AnyAsync(rp => rp.RoleID == userRoleId))
            {
                return;
            }

            var viewOnlyKeys = new HashSet<string>
            {
                PageKeys.Dashboard,
                PageKeys.SalesInvoices,
                PageKeys.Customers,
                PageKeys.Products
            };

            var pages = await context.ApplicationPages
                .Where(p => p.IsActive && viewOnlyKeys.Contains(p.PageKey))
                .ToListAsync();

            foreach (var page in pages)
            {
                await context.RolePermissions.AddAsync(new RolePermission
                {
                    RoleID = userRoleId,
                    ApplicationPageID = page.ApplicationPageID,
                    CanView = true,
                    CanAdd = false,
                    CanEdit = false,
                    CanDelete = false
                });
            }

            await context.SaveChangesAsync();
        }
    }
}

