using System.Collections.Generic;
using InventorySystem.Constants;
using InventorySystem.Models.Entities;

namespace InventorySystem.Data
{
    public static class PermissionSeedData
    {
        public static IReadOnlyList<(string ModuleKey, string ModuleName, int Order, IReadOnlyList<(string PageKey, string PageName, string? Controller, string? Action, int PageOrder)> Pages)> GetModulesAndPages()
        {
            return new List<(string, string, int, IReadOnlyList<(string, string, string?, string?, int)>)>
            {
                ("Dashboard", "Dashboard", 1, new List<(string, string, string?, string?, int)>
                {
                    (PageKeys.Dashboard, "Dashboard", "Home", "Index", 1)
                }),
                ("Sales", "Sales", 2, new List<(string, string, string?, string?, int)>
                {
                    (PageKeys.SalesInvoices, "Sales Invoices", "Sales", "Index", 1),
                    (PageKeys.SalesCreate, "Create Sale", "Sales", "Create", 2),
                    (PageKeys.LoadSheets, "Load Sheets", "LoadSheets", "Index", 3),
                    (PageKeys.Customers, "Customers", "Customers", "Index", 4),
                    (PageKeys.CustomerPayments, "Customer Payments", "Payments", "CustomerPayments", 5),
                    (PageKeys.SalesReturns, "Sales Returns", "Returns", "CustomerReturns", 6),
                    (PageKeys.SalesCreateReturn, "Create Return", "Returns", "CreateReturn", 7)
                }),
                ("Purchasing", "Purchasing", 3, new List<(string, string, string?, string?, int)>
                {
                    (PageKeys.PurchaseInvoices, "Purchase Invoices", "Purchases", "Index", 1),
                    (PageKeys.PurchaseCreate, "Create Purchase", "Purchases", "Create", 2),
                    (PageKeys.PurchaseReturns, "Purchase Returns", "Returns", "CompanyReturns", 3),
                    (PageKeys.CompanyPayments, "Company Payments", "Payments", "CompanyPayments", 4)
                }),
                ("Inventory", "Inventory", 4, new List<(string, string, string?, string?, int)>
                {
                    (PageKeys.Products, "Products", "Products", "Index", 1),
                    (PageKeys.Categories, "Categories", "Categories", "Index", 2),
                    (PageKeys.CompanyStockReport, "Company Stock Report", "CompanyStockReport", "Index", 3)
                }),
                ("Masters", "Masters", 5, new List<(string, string, string?, string?, int)>
                {
                    (PageKeys.Companies, "Companies", "Companies", "Index", 1),
                    (PageKeys.Bookers, "Bookers", "Bookers", "Index", 2),
                    (PageKeys.Suppliers, "Suppliers", "Suppliers", "Index", 3)
                }),
                ("Promotions", "Promotions", 6, new List<(string, string, string?, string?, int)>
                {
                    (PageKeys.Promotions, "Promotions", "Promotions", "Index", 1),
                    (PageKeys.DiscountRules, "Discount Rules", "Discounts", "Index", 2)
                }),
                ("Accounting", "Accounting", 7, new List<(string, string, string?, string?, int)>
                {
                    (PageKeys.CustomerLedger, "Customer Ledger", "Customers", "Index", 8),
                    (PageKeys.CompanyLedger, "Company Ledger", "Companies", "Details", 9),
                    (PageKeys.Cheques, "Cheque Management", "Payments", "Cheques", 10)
                }),
                ("Analytics", "Analytics", 8, new List<(string, string, string?, string?, int)>
                {
                    (PageKeys.Analytics, "Analytics & Insights", "Analytics", "Index", 1)
                }),
                ("Administration", "Administration", 9, new List<(string, string, string?, string?, int)>
                {
                    (PageKeys.Users, "Users", "UserManagement", "Index", 1),
                    (PageKeys.Roles, "Roles", "RoleManagement", "Index", 2)
                })
            };
        }
    }
}
