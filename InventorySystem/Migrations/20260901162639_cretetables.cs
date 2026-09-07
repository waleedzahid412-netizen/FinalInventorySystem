using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventorySystem.Migrations
{
    /// <inheritdoc />
    public partial class cretetables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "dbo");

            migrationBuilder.CreateTable(
                name: "ApplicationModules",
                schema: "dbo",
                columns: table => new
                {
                    ApplicationModuleID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ModuleKey = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ModuleName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationModules", x => x.ApplicationModuleID);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationPages",
                schema: "dbo",
                columns: table => new
                {
                    ApplicationPageID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ApplicationModuleID = table.Column<int>(type: "int", nullable: false),
                    PageKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    PageName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ControllerName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    DefaultActionName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationPages", x => x.ApplicationPageID);
                    table.ForeignKey(
                        name: "FK_ApplicationPages_ApplicationModules_ApplicationModuleID",
                        column: x => x.ApplicationModuleID,
                        principalSchema: "dbo",
                        principalTable: "ApplicationModules",
                        principalColumn: "ApplicationModuleID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Areas",
                schema: "dbo",
                columns: table => new
                {
                    AreaID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AreaName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Areas", x => x.AreaID);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                schema: "dbo",
                columns: table => new
                {
                    AuditID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserID = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TableName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RecordID = table.Column<int>(type: "int", nullable: false),
                    OldValues = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewValues = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ActionDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.AuditID);
                });

            migrationBuilder.CreateTable(
                name: "Bookers",
                schema: "dbo",
                columns: table => new
                {
                    BookerID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CNIC = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CreditLimit = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bookers", x => x.BookerID);
                });

            migrationBuilder.CreateTable(
                name: "Categories",
                schema: "dbo",
                columns: table => new
                {
                    CategoryID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.CategoryID);
                });

            migrationBuilder.CreateTable(
                name: "ChequeStatusAudits",
                schema: "dbo",
                columns: table => new
                {
                    ChequeStatusAuditID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PaymentType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PaymentID = table.Column<int>(type: "int", nullable: false),
                    PreviousStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NewStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChequeStatusAudits", x => x.ChequeStatusAuditID);
                });

            migrationBuilder.CreateTable(
                name: "Companies",
                schema: "dbo",
                columns: table => new
                {
                    CompanyID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ContactPerson = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    TaxID = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreditLimit = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CompanyPercentage = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Companies", x => x.CompanyID);
                });

            migrationBuilder.CreateTable(
                name: "CompanyLedger",
                schema: "dbo",
                columns: table => new
                {
                    CompanyLedgerID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TransactionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TransactionType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    DebitAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreditAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PurchaseInvoiceID = table.Column<int>(type: "int", nullable: true),
                    CompanyPaymentID = table.Column<int>(type: "int", nullable: true),
                    PurchaseReturnID = table.Column<int>(type: "int", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyLedger", x => x.CompanyLedgerID);
                    table.ForeignKey(
                        name: "FK_CompanyLedger_Companies_CompanyID",
                        column: x => x.CompanyID,
                        principalSchema: "dbo",
                        principalTable: "Companies",
                        principalColumn: "CompanyID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CompanyPayments",
                schema: "dbo",
                columns: table => new
                {
                    CompanyPaymentID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    PurchaseInvoiceID = table.Column<int>(type: "int", nullable: false),
                    PaymentDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaymentMethod = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PaidBy = table.Column<int>(type: "int", nullable: false),
                    ChequeNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    BankName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IssueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ChequeDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ChequeStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ClearedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClearedBy = table.Column<int>(type: "int", nullable: true),
                    BouncedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BounceRemarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyPayments", x => x.CompanyPaymentID);
                    table.ForeignKey(
                        name: "FK_CompanyPayments_Companies_CompanyID",
                        column: x => x.CompanyID,
                        principalSchema: "dbo",
                        principalTable: "Companies",
                        principalColumn: "CompanyID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomerLedger",
                schema: "dbo",
                columns: table => new
                {
                    CustomerLedgerID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerID = table.Column<int>(type: "int", nullable: false),
                    TransactionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TransactionType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    DebitAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreditAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SalesInvoiceID = table.Column<int>(type: "int", nullable: true),
                    CustomerPaymentID = table.Column<int>(type: "int", nullable: true),
                    SalesReturnID = table.Column<int>(type: "int", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerLedger", x => x.CustomerLedgerID);
                });

            migrationBuilder.CreateTable(
                name: "CustomerPayments",
                schema: "dbo",
                columns: table => new
                {
                    CustomerPaymentID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerID = table.Column<int>(type: "int", nullable: false),
                    InvoiceID = table.Column<int>(type: "int", nullable: false),
                    PaymentDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaymentMethod = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ReceivedBy = table.Column<int>(type: "int", nullable: false),
                    ChequeNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    BankName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IssueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ChequeDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ChequeStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ClearedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClearedBy = table.Column<int>(type: "int", nullable: true),
                    BouncedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BounceRemarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerPayments", x => x.CustomerPaymentID);
                });

            migrationBuilder.CreateTable(
                name: "Customers",
                schema: "dbo",
                columns: table => new
                {
                    CustomerID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ShopName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    OwnerName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    AreaID = table.Column<int>(type: "int", nullable: true),
                    SubAreaID = table.Column<int>(type: "int", nullable: true),
                    TaxID = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreditLimit = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PreferredDiscountPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.CustomerID);
                    table.ForeignKey(
                        name: "FK_Customers_Areas_AreaID",
                        column: x => x.AreaID,
                        principalSchema: "dbo",
                        principalTable: "Areas",
                        principalColumn: "AreaID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DiscountRules",
                schema: "dbo",
                columns: table => new
                {
                    DiscountRuleID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RuleName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MinimumOrderAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MaximumOrderAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    DiscountType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DiscountValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscountRules", x => x.DiscountRuleID);
                    table.ForeignKey(
                        name: "FK_DiscountRules_Companies_CompanyID",
                        column: x => x.CompanyID,
                        principalSchema: "dbo",
                        principalTable: "Companies",
                        principalColumn: "CompanyID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryCostConsumptions",
                schema: "dbo",
                columns: table => new
                {
                    ConsumptionID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CostLayerID = table.Column<int>(type: "int", nullable: false),
                    SalesInvoiceItemID = table.Column<int>(type: "int", nullable: true),
                    TransactionType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    UnitCostInBase = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    TotalCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryCostConsumptions", x => x.ConsumptionID);
                });

            migrationBuilder.CreateTable(
                name: "InventoryCostLayers",
                schema: "dbo",
                columns: table => new
                {
                    CostLayerID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductID = table.Column<int>(type: "int", nullable: false),
                    WarehouseID = table.Column<int>(type: "int", nullable: false),
                    PurchaseInvoiceItemID = table.Column<int>(type: "int", nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SourceType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    OriginalQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    RemainingQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    UnitCostInBase = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    SalesReturnItemID = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryCostLayers", x => x.CostLayerID);
                });

            migrationBuilder.CreateTable(
                name: "InventoryStock",
                schema: "dbo",
                columns: table => new
                {
                    InventoryStockID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductID = table.Column<int>(type: "int", nullable: false),
                    WarehouseID = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    DamagedQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryStock", x => x.InventoryStockID);
                });

            migrationBuilder.CreateTable(
                name: "InventoryTransactions",
                schema: "dbo",
                columns: table => new
                {
                    TransactionID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductID = table.Column<int>(type: "int", nullable: false),
                    WarehouseID = table.Column<int>(type: "int", nullable: false),
                    TransactionType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PurchaseInvoiceItemID = table.Column<int>(type: "int", nullable: true),
                    SalesInvoiceItemID = table.Column<int>(type: "int", nullable: true),
                    SalesReturnItemID = table.Column<int>(type: "int", nullable: true),
                    PurchaseReturnItemID = table.Column<int>(type: "int", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryTransactions", x => x.TransactionID);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceDiscounts",
                schema: "dbo",
                columns: table => new
                {
                    InvoiceDiscountID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InvoiceID = table.Column<int>(type: "int", nullable: false),
                    DiscountRuleID = table.Column<int>(type: "int", nullable: true),
                    RuleName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DiscountType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DiscountValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MinimumOrderAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    MaximumOrderAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    DiscountSource = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AppliedBy = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceDiscounts", x => x.InvoiceDiscountID);
                    table.ForeignKey(
                        name: "FK_InvoiceDiscounts_DiscountRules_DiscountRuleID",
                        column: x => x.DiscountRuleID,
                        principalSchema: "dbo",
                        principalTable: "DiscountRules",
                        principalColumn: "DiscountRuleID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceEditAudits",
                schema: "dbo",
                columns: table => new
                {
                    InvoiceEditAuditID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InvoiceID = table.Column<int>(type: "int", nullable: false),
                    InvoiceType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PreviousVersion = table.Column<int>(type: "int", nullable: false),
                    NewVersion = table.Column<int>(type: "int", nullable: false),
                    PreviousItemCount = table.Column<int>(type: "int", nullable: false),
                    NewItemCount = table.Column<int>(type: "int", nullable: false),
                    OldSubTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NewSubTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OldDiscountTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NewDiscountTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OldGrandTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NewGrandTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    EditReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    EditedBy = table.Column<int>(type: "int", nullable: false),
                    EditedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceEditAudits", x => x.InvoiceEditAuditID);
                });

            migrationBuilder.CreateTable(
                name: "InvoicePromotions",
                schema: "dbo",
                columns: table => new
                {
                    InvoicePromotionID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InvoiceID = table.Column<int>(type: "int", nullable: false),
                    PromotionID = table.Column<int>(type: "int", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AppliedBy = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoicePromotions", x => x.InvoicePromotionID);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                schema: "dbo",
                columns: table => new
                {
                    ProductID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CategoryID = table.Column<int>(type: "int", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    BaseUnitID = table.Column<int>(type: "int", nullable: false),
                    ProductName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    SKU = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Barcode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BaseSellingPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AveragePurchaseCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ReorderLevel = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.ProductID);
                    table.ForeignKey(
                        name: "FK_Products_Categories_CategoryID",
                        column: x => x.CategoryID,
                        principalSchema: "dbo",
                        principalTable: "Categories",
                        principalColumn: "CategoryID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Products_Companies_CompanyID",
                        column: x => x.CompanyID,
                        principalSchema: "dbo",
                        principalTable: "Companies",
                        principalColumn: "CompanyID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductUnits",
                schema: "dbo",
                columns: table => new
                {
                    ProductUnitID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductID = table.Column<int>(type: "int", nullable: false),
                    UnitID = table.Column<int>(type: "int", nullable: false),
                    ConversionToBaseUnit = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    PurchasePrice = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    SellingPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    IsDefaultPurchaseUnit = table.Column<bool>(type: "bit", nullable: false),
                    IsDefaultSalesUnit = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductUnits", x => x.ProductUnitID);
                    table.ForeignKey(
                        name: "FK_ProductUnits_Products_ProductID",
                        column: x => x.ProductID,
                        principalSchema: "dbo",
                        principalTable: "Products",
                        principalColumn: "ProductID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PromotionCampaigns",
                schema: "dbo",
                columns: table => new
                {
                    PromotionID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromotionCampaigns", x => x.PromotionID);
                    table.ForeignKey(
                        name: "FK_PromotionCampaigns_Companies_CompanyID",
                        column: x => x.CompanyID,
                        principalSchema: "dbo",
                        principalTable: "Companies",
                        principalColumn: "CompanyID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PromotionRules",
                schema: "dbo",
                columns: table => new
                {
                    RuleID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PromotionID = table.Column<int>(type: "int", nullable: false),
                    BuyProductID = table.Column<int>(type: "int", nullable: false),
                    BuyQuantity = table.Column<int>(type: "int", nullable: false),
                    FreeProductID = table.Column<int>(type: "int", nullable: true),
                    IsCustomFreeItem = table.Column<bool>(type: "bit", nullable: false),
                    CustomFreeItemName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FreeQuantity = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromotionRules", x => x.RuleID);
                    table.ForeignKey(
                        name: "FK_PromotionRules_Products_BuyProductID",
                        column: x => x.BuyProductID,
                        principalSchema: "dbo",
                        principalTable: "Products",
                        principalColumn: "ProductID");
                    table.ForeignKey(
                        name: "FK_PromotionRules_Products_FreeProductID",
                        column: x => x.FreeProductID,
                        principalSchema: "dbo",
                        principalTable: "Products",
                        principalColumn: "ProductID");
                    table.ForeignKey(
                        name: "FK_PromotionRules_PromotionCampaigns_PromotionID",
                        column: x => x.PromotionID,
                        principalSchema: "dbo",
                        principalTable: "PromotionCampaigns",
                        principalColumn: "PromotionID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseInvoiceItems",
                schema: "dbo",
                columns: table => new
                {
                    PurchaseItemID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PurchaseInvoiceID = table.Column<int>(type: "int", nullable: false),
                    ProductID = table.Column<int>(type: "int", nullable: false),
                    ProductUnitID = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    ConvertedQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseInvoiceItems", x => x.PurchaseItemID);
                    table.ForeignKey(
                        name: "FK_PurchaseInvoiceItems_ProductUnits_ProductUnitID",
                        column: x => x.ProductUnitID,
                        principalSchema: "dbo",
                        principalTable: "ProductUnits",
                        principalColumn: "ProductUnitID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseInvoiceItems_Products_ProductID",
                        column: x => x.ProductID,
                        principalSchema: "dbo",
                        principalTable: "Products",
                        principalColumn: "ProductID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseInvoices",
                schema: "dbo",
                columns: table => new
                {
                    PurchaseInvoiceID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    WarehouseID = table.Column<int>(type: "int", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SupplierInvoiceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    InvoiceDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SubTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    GrandTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaidAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaymentStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseInvoices", x => x.PurchaseInvoiceID);
                    table.ForeignKey(
                        name: "FK_PurchaseInvoices_Companies_CompanyID",
                        column: x => x.CompanyID,
                        principalSchema: "dbo",
                        principalTable: "Companies",
                        principalColumn: "CompanyID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseReturnItems",
                schema: "dbo",
                columns: table => new
                {
                    PurchaseReturnItemID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PurchaseReturnID = table.Column<int>(type: "int", nullable: false),
                    PurchaseInvoiceItemID = table.Column<int>(type: "int", nullable: false),
                    ProductID = table.Column<int>(type: "int", nullable: false),
                    ProductUnitID = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    ConvertedQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    RefundUnitCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RefundAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ReturnCondition = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseReturnItems", x => x.PurchaseReturnItemID);
                    table.ForeignKey(
                        name: "FK_PurchaseReturnItems_ProductUnits_ProductUnitID",
                        column: x => x.ProductUnitID,
                        principalSchema: "dbo",
                        principalTable: "ProductUnits",
                        principalColumn: "ProductUnitID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PurchaseReturnItems_Products_ProductID",
                        column: x => x.ProductID,
                        principalSchema: "dbo",
                        principalTable: "Products",
                        principalColumn: "ProductID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PurchaseReturnItems_PurchaseInvoiceItems_PurchaseInvoiceItemID",
                        column: x => x.PurchaseInvoiceItemID,
                        principalSchema: "dbo",
                        principalTable: "PurchaseInvoiceItems",
                        principalColumn: "PurchaseItemID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseReturns",
                schema: "dbo",
                columns: table => new
                {
                    PurchaseReturnID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReturnNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PurchaseInvoiceID = table.Column<int>(type: "int", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    WarehouseID = table.Column<int>(type: "int", nullable: false),
                    ReturnDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    GrossAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NetRefundAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseReturns", x => x.PurchaseReturnID);
                    table.ForeignKey(
                        name: "FK_PurchaseReturns_Companies_CompanyID",
                        column: x => x.CompanyID,
                        principalSchema: "dbo",
                        principalTable: "Companies",
                        principalColumn: "CompanyID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PurchaseReturns_PurchaseInvoices_PurchaseInvoiceID",
                        column: x => x.PurchaseInvoiceID,
                        principalSchema: "dbo",
                        principalTable: "PurchaseInvoices",
                        principalColumn: "PurchaseInvoiceID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QuotationItems",
                schema: "dbo",
                columns: table => new
                {
                    QuotationItemID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QuotationID = table.Column<int>(type: "int", nullable: false),
                    ProductID = table.Column<int>(type: "int", nullable: false),
                    ProductUnitID = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuotationItems", x => x.QuotationItemID);
                    table.ForeignKey(
                        name: "FK_QuotationItems_ProductUnits_ProductUnitID",
                        column: x => x.ProductUnitID,
                        principalSchema: "dbo",
                        principalTable: "ProductUnits",
                        principalColumn: "ProductUnitID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuotationItems_Products_ProductID",
                        column: x => x.ProductID,
                        principalSchema: "dbo",
                        principalTable: "Products",
                        principalColumn: "ProductID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Quotations",
                schema: "dbo",
                columns: table => new
                {
                    QuotationID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerID = table.Column<int>(type: "int", nullable: false),
                    QuotationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Quotations", x => x.QuotationID);
                    table.ForeignKey(
                        name: "FK_Quotations_Customers_CustomerID",
                        column: x => x.CustomerID,
                        principalSchema: "dbo",
                        principalTable: "Customers",
                        principalColumn: "CustomerID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                schema: "dbo",
                columns: table => new
                {
                    RolePermissionID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleID = table.Column<int>(type: "int", nullable: false),
                    ApplicationPageID = table.Column<int>(type: "int", nullable: false),
                    CanView = table.Column<bool>(type: "bit", nullable: false),
                    CanAdd = table.Column<bool>(type: "bit", nullable: false),
                    CanEdit = table.Column<bool>(type: "bit", nullable: false),
                    CanDelete = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => x.RolePermissionID);
                    table.ForeignKey(
                        name: "FK_RolePermissions_ApplicationPages_ApplicationPageID",
                        column: x => x.ApplicationPageID,
                        principalSchema: "dbo",
                        principalTable: "ApplicationPages",
                        principalColumn: "ApplicationPageID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                schema: "dbo",
                columns: table => new
                {
                    RoleID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RoleDescription = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    IsSystemRole = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.RoleID);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                schema: "dbo",
                columns: table => new
                {
                    UserID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleID = table.Column<int>(type: "int", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Username = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.UserID);
                    table.ForeignKey(
                        name: "FK_Users_Roles_RoleID",
                        column: x => x.RoleID,
                        principalSchema: "dbo",
                        principalTable: "Roles",
                        principalColumn: "RoleID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Users_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "UserID");
                    table.ForeignKey(
                        name: "FK_Users_Users_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "UserID");
                });

            migrationBuilder.CreateTable(
                name: "SubAreas",
                schema: "dbo",
                columns: table => new
                {
                    SubAreaID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AreaID = table.Column<int>(type: "int", nullable: false),
                    SubAreaName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubAreas", x => x.SubAreaID);
                    table.ForeignKey(
                        name: "FK_SubAreas_Areas_AreaID",
                        column: x => x.AreaID,
                        principalSchema: "dbo",
                        principalTable: "Areas",
                        principalColumn: "AreaID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubAreas_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "UserID");
                    table.ForeignKey(
                        name: "FK_SubAreas_Users_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "UserID");
                });

            migrationBuilder.CreateTable(
                name: "Suppliers",
                schema: "dbo",
                columns: table => new
                {
                    SupplierID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CNIC = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Type = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Suppliers", x => x.SupplierID);
                    table.ForeignKey(
                        name: "FK_Suppliers_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "UserID");
                    table.ForeignKey(
                        name: "FK_Suppliers_Users_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "UserID");
                });

            migrationBuilder.CreateTable(
                name: "Units",
                schema: "dbo",
                columns: table => new
                {
                    UnitID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UnitName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Units", x => x.UnitID);
                    table.ForeignKey(
                        name: "FK_Units_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "UserID");
                    table.ForeignKey(
                        name: "FK_Units_Users_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "UserID");
                });

            migrationBuilder.CreateTable(
                name: "UserCompanies",
                schema: "dbo",
                columns: table => new
                {
                    UserCompanyID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserID = table.Column<int>(type: "int", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserCompanies", x => x.UserCompanyID);
                    table.ForeignKey(
                        name: "FK_UserCompanies_Companies_CompanyID",
                        column: x => x.CompanyID,
                        principalSchema: "dbo",
                        principalTable: "Companies",
                        principalColumn: "CompanyID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserCompanies_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "UserID");
                    table.ForeignKey(
                        name: "FK_UserCompanies_Users_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "UserID");
                    table.ForeignKey(
                        name: "FK_UserCompanies_Users_UserID",
                        column: x => x.UserID,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Warehouses",
                schema: "dbo",
                columns: table => new
                {
                    WarehouseID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsMain = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Warehouses", x => x.WarehouseID);
                    table.ForeignKey(
                        name: "FK_Warehouses_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "UserID");
                    table.ForeignKey(
                        name: "FK_Warehouses_Users_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "UserID");
                });

            migrationBuilder.CreateTable(
                name: "SalesInvoices",
                schema: "dbo",
                columns: table => new
                {
                    InvoiceID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerID = table.Column<int>(type: "int", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    BookerID = table.Column<int>(type: "int", nullable: true),
                    SalespersonID = table.Column<int>(type: "int", nullable: true),
                    WarehouseID = table.Column<int>(type: "int", nullable: false),
                    AreaID = table.Column<int>(type: "int", nullable: true),
                    SubAreaID = table.Column<int>(type: "int", nullable: true),
                    SupplierID = table.Column<int>(type: "int", nullable: true),
                    QuotationID = table.Column<int>(type: "int", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    InvoiceDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SubTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiscountTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiscountMode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TaxTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    GrandTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaidAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaymentStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    IsLocked = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesInvoices", x => x.InvoiceID);
                    table.ForeignKey(
                        name: "FK_SalesInvoices_Areas_AreaID",
                        column: x => x.AreaID,
                        principalSchema: "dbo",
                        principalTable: "Areas",
                        principalColumn: "AreaID");
                    table.ForeignKey(
                        name: "FK_SalesInvoices_Bookers_BookerID",
                        column: x => x.BookerID,
                        principalSchema: "dbo",
                        principalTable: "Bookers",
                        principalColumn: "BookerID");
                    table.ForeignKey(
                        name: "FK_SalesInvoices_Companies_CompanyID",
                        column: x => x.CompanyID,
                        principalSchema: "dbo",
                        principalTable: "Companies",
                        principalColumn: "CompanyID");
                    table.ForeignKey(
                        name: "FK_SalesInvoices_Customers_CustomerID",
                        column: x => x.CustomerID,
                        principalSchema: "dbo",
                        principalTable: "Customers",
                        principalColumn: "CustomerID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesInvoices_Quotations_QuotationID",
                        column: x => x.QuotationID,
                        principalSchema: "dbo",
                        principalTable: "Quotations",
                        principalColumn: "QuotationID");
                    table.ForeignKey(
                        name: "FK_SalesInvoices_SubAreas_SubAreaID",
                        column: x => x.SubAreaID,
                        principalSchema: "dbo",
                        principalTable: "SubAreas",
                        principalColumn: "SubAreaID");
                    table.ForeignKey(
                        name: "FK_SalesInvoices_Suppliers_SupplierID",
                        column: x => x.SupplierID,
                        principalSchema: "dbo",
                        principalTable: "Suppliers",
                        principalColumn: "SupplierID");
                    table.ForeignKey(
                        name: "FK_SalesInvoices_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "UserID");
                    table.ForeignKey(
                        name: "FK_SalesInvoices_Users_SalespersonID",
                        column: x => x.SalespersonID,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "UserID");
                    table.ForeignKey(
                        name: "FK_SalesInvoices_Users_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "UserID");
                    table.ForeignKey(
                        name: "FK_SalesInvoices_Warehouses_WarehouseID",
                        column: x => x.WarehouseID,
                        principalSchema: "dbo",
                        principalTable: "Warehouses",
                        principalColumn: "WarehouseID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SalesInvoiceItems",
                schema: "dbo",
                columns: table => new
                {
                    InvoiceItemID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InvoiceID = table.Column<int>(type: "int", nullable: false),
                    ProductID = table.Column<int>(type: "int", nullable: true),
                    ProductUnitID = table.Column<int>(type: "int", nullable: true),
                    CustomItemName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    ConvertedQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiscountRate = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CostOfGoodsSold = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UnitCostInBase = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ItemType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PromotionID = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesInvoiceItems", x => x.InvoiceItemID);
                    table.ForeignKey(
                        name: "FK_SalesInvoiceItems_ProductUnits_ProductUnitID",
                        column: x => x.ProductUnitID,
                        principalSchema: "dbo",
                        principalTable: "ProductUnits",
                        principalColumn: "ProductUnitID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesInvoiceItems_Products_ProductID",
                        column: x => x.ProductID,
                        principalSchema: "dbo",
                        principalTable: "Products",
                        principalColumn: "ProductID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesInvoiceItems_PromotionCampaigns_PromotionID",
                        column: x => x.PromotionID,
                        principalSchema: "dbo",
                        principalTable: "PromotionCampaigns",
                        principalColumn: "PromotionID");
                    table.ForeignKey(
                        name: "FK_SalesInvoiceItems_SalesInvoices_InvoiceID",
                        column: x => x.InvoiceID,
                        principalSchema: "dbo",
                        principalTable: "SalesInvoices",
                        principalColumn: "InvoiceID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesInvoiceItems_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "UserID");
                    table.ForeignKey(
                        name: "FK_SalesInvoiceItems_Users_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "UserID");
                });

            migrationBuilder.CreateTable(
                name: "SalesReturns",
                schema: "dbo",
                columns: table => new
                {
                    SalesReturnID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReturnNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ReturnType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    InvoiceID = table.Column<int>(type: "int", nullable: true),
                    CustomerID = table.Column<int>(type: "int", nullable: false),
                    WarehouseID = table.Column<int>(type: "int", nullable: true),
                    SettlementMethod = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ReturnDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    GrossAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ClawbackPenalty = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PromoPenalty = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NetRefundAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IncludeSchemeCalculation = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesReturns", x => x.SalesReturnID);
                    table.ForeignKey(
                        name: "FK_SalesReturns_Customers_CustomerID",
                        column: x => x.CustomerID,
                        principalSchema: "dbo",
                        principalTable: "Customers",
                        principalColumn: "CustomerID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesReturns_SalesInvoices_InvoiceID",
                        column: x => x.InvoiceID,
                        principalSchema: "dbo",
                        principalTable: "SalesInvoices",
                        principalColumn: "InvoiceID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesReturns_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "UserID");
                    table.ForeignKey(
                        name: "FK_SalesReturns_Warehouses_WarehouseID",
                        column: x => x.WarehouseID,
                        principalSchema: "dbo",
                        principalTable: "Warehouses",
                        principalColumn: "WarehouseID");
                });

            migrationBuilder.CreateTable(
                name: "SalesReturnItems",
                schema: "dbo",
                columns: table => new
                {
                    SalesReturnItemID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SalesReturnID = table.Column<int>(type: "int", nullable: false),
                    InvoiceItemID = table.Column<int>(type: "int", nullable: true),
                    ProductID = table.Column<int>(type: "int", nullable: true),
                    ProductUnitID = table.Column<int>(type: "int", nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    ConvertedQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    RefundUnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RefundAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PromoPenaltyQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    PromoPenaltyAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ReturnCondition = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesReturnItems", x => x.SalesReturnItemID);
                    table.ForeignKey(
                        name: "FK_SalesReturnItems_ProductUnits_ProductUnitID",
                        column: x => x.ProductUnitID,
                        principalSchema: "dbo",
                        principalTable: "ProductUnits",
                        principalColumn: "ProductUnitID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesReturnItems_Products_ProductID",
                        column: x => x.ProductID,
                        principalSchema: "dbo",
                        principalTable: "Products",
                        principalColumn: "ProductID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesReturnItems_SalesInvoiceItems_InvoiceItemID",
                        column: x => x.InvoiceItemID,
                        principalSchema: "dbo",
                        principalTable: "SalesInvoiceItems",
                        principalColumn: "InvoiceItemID");
                    table.ForeignKey(
                        name: "FK_SalesReturnItems_SalesReturns_SalesReturnID",
                        column: x => x.SalesReturnID,
                        principalSchema: "dbo",
                        principalTable: "SalesReturns",
                        principalColumn: "SalesReturnID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationModules_ModuleKey",
                schema: "dbo",
                table: "ApplicationModules",
                column: "ModuleKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationPages_ApplicationModuleID",
                schema: "dbo",
                table: "ApplicationPages",
                column: "ApplicationModuleID");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationPages_PageKey",
                schema: "dbo",
                table: "ApplicationPages",
                column: "PageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Areas_AreaName",
                schema: "dbo",
                table: "Areas",
                column: "AreaName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Areas_Code",
                schema: "dbo",
                table: "Areas",
                column: "Code",
                unique: true,
                filter: "[Code] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Areas_CreatedBy",
                schema: "dbo",
                table: "Areas",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Areas_UpdatedBy",
                schema: "dbo",
                table: "Areas",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserID",
                schema: "dbo",
                table: "AuditLogs",
                column: "UserID");

            migrationBuilder.CreateIndex(
                name: "IX_Bookers_CompanyID",
                schema: "dbo",
                table: "Bookers",
                column: "CompanyID");

            migrationBuilder.CreateIndex(
                name: "IX_Bookers_CreatedBy",
                schema: "dbo",
                table: "Bookers",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Bookers_UpdatedBy",
                schema: "dbo",
                table: "Bookers",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "UX_Bookers_CNIC",
                schema: "dbo",
                table: "Bookers",
                column: "CNIC",
                unique: true,
                filter: "[IsDeleted] = 0 AND [CNIC] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Bookers_CompanyID_Name",
                schema: "dbo",
                table: "Bookers",
                columns: new[] { "CompanyID", "Name" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_CompanyID_Name",
                schema: "dbo",
                table: "Categories",
                columns: new[] { "CompanyID", "Name" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_CreatedBy",
                schema: "dbo",
                table: "Categories",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_UpdatedBy",
                schema: "dbo",
                table: "Categories",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ChequeStatusAudits_UpdatedBy",
                schema: "dbo",
                table: "ChequeStatusAudits",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Companies_UpdatedBy",
                schema: "dbo",
                table: "Companies",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyLedger_CompanyID",
                schema: "dbo",
                table: "CompanyLedger",
                column: "CompanyID");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyLedger_CompanyPaymentID",
                schema: "dbo",
                table: "CompanyLedger",
                column: "CompanyPaymentID",
                unique: true,
                filter: "[CompanyPaymentID] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyLedger_CreatedBy",
                schema: "dbo",
                table: "CompanyLedger",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyLedger_PurchaseInvoiceID",
                schema: "dbo",
                table: "CompanyLedger",
                column: "PurchaseInvoiceID");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyLedger_PurchaseReturnID",
                schema: "dbo",
                table: "CompanyLedger",
                column: "PurchaseReturnID");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyPayments_CompanyID",
                schema: "dbo",
                table: "CompanyPayments",
                column: "CompanyID");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyPayments_PaidBy",
                schema: "dbo",
                table: "CompanyPayments",
                column: "PaidBy");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyPayments_PurchaseInvoiceID",
                schema: "dbo",
                table: "CompanyPayments",
                column: "PurchaseInvoiceID");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerLedger_CreatedBy",
                schema: "dbo",
                table: "CustomerLedger",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerLedger_CustomerID",
                schema: "dbo",
                table: "CustomerLedger",
                column: "CustomerID");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerLedger_CustomerPaymentID",
                schema: "dbo",
                table: "CustomerLedger",
                column: "CustomerPaymentID",
                unique: true,
                filter: "[CustomerPaymentID] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerLedger_SalesInvoiceID",
                schema: "dbo",
                table: "CustomerLedger",
                column: "SalesInvoiceID");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerLedger_SalesReturnID",
                schema: "dbo",
                table: "CustomerLedger",
                column: "SalesReturnID");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPayments_CustomerID",
                schema: "dbo",
                table: "CustomerPayments",
                column: "CustomerID");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPayments_InvoiceID",
                schema: "dbo",
                table: "CustomerPayments",
                column: "InvoiceID");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPayments_ReceivedBy",
                schema: "dbo",
                table: "CustomerPayments",
                column: "ReceivedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_AreaID",
                schema: "dbo",
                table: "Customers",
                column: "AreaID");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_CreatedBy",
                schema: "dbo",
                table: "Customers",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_SubAreaID",
                schema: "dbo",
                table: "Customers",
                column: "SubAreaID");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_UpdatedBy",
                schema: "dbo",
                table: "Customers",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_DiscountRules_CompanyID",
                schema: "dbo",
                table: "DiscountRules",
                column: "CompanyID");

            migrationBuilder.CreateIndex(
                name: "IX_DiscountRules_CreatedBy",
                schema: "dbo",
                table: "DiscountRules",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCostConsumptions_CostLayerID",
                schema: "dbo",
                table: "InventoryCostConsumptions",
                column: "CostLayerID");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCostConsumptions_CreatedBy",
                schema: "dbo",
                table: "InventoryCostConsumptions",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCostConsumptions_SalesInvoiceItemID",
                schema: "dbo",
                table: "InventoryCostConsumptions",
                column: "SalesInvoiceItemID");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCostLayers_CreatedBy",
                schema: "dbo",
                table: "InventoryCostLayers",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCostLayers_ProductID_WarehouseID_ReceivedAt",
                schema: "dbo",
                table: "InventoryCostLayers",
                columns: new[] { "ProductID", "WarehouseID", "ReceivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCostLayers_PurchaseInvoiceItemID",
                schema: "dbo",
                table: "InventoryCostLayers",
                column: "PurchaseInvoiceItemID");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCostLayers_SalesReturnItemID",
                schema: "dbo",
                table: "InventoryCostLayers",
                column: "SalesReturnItemID");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCostLayers_WarehouseID",
                schema: "dbo",
                table: "InventoryCostLayers",
                column: "WarehouseID");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryStock_CreatedBy",
                schema: "dbo",
                table: "InventoryStock",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryStock_ProductID_WarehouseID",
                schema: "dbo",
                table: "InventoryStock",
                columns: new[] { "ProductID", "WarehouseID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryStock_UpdatedBy",
                schema: "dbo",
                table: "InventoryStock",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryStock_WarehouseID",
                schema: "dbo",
                table: "InventoryStock",
                column: "WarehouseID");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_CreatedBy",
                schema: "dbo",
                table: "InventoryTransactions",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_ProductID",
                schema: "dbo",
                table: "InventoryTransactions",
                column: "ProductID");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_PurchaseInvoiceItemID",
                schema: "dbo",
                table: "InventoryTransactions",
                column: "PurchaseInvoiceItemID");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_PurchaseReturnItemID",
                schema: "dbo",
                table: "InventoryTransactions",
                column: "PurchaseReturnItemID");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_SalesInvoiceItemID",
                schema: "dbo",
                table: "InventoryTransactions",
                column: "SalesInvoiceItemID");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_SalesReturnItemID",
                schema: "dbo",
                table: "InventoryTransactions",
                column: "SalesReturnItemID");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_UpdatedBy",
                schema: "dbo",
                table: "InventoryTransactions",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_WarehouseID",
                schema: "dbo",
                table: "InventoryTransactions",
                column: "WarehouseID");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceDiscounts_AppliedBy",
                schema: "dbo",
                table: "InvoiceDiscounts",
                column: "AppliedBy");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceDiscounts_DiscountRuleID",
                schema: "dbo",
                table: "InvoiceDiscounts",
                column: "DiscountRuleID");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceDiscounts_InvoiceID",
                schema: "dbo",
                table: "InvoiceDiscounts",
                column: "InvoiceID");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceEditAudits_EditedBy",
                schema: "dbo",
                table: "InvoiceEditAudits",
                column: "EditedBy");

            migrationBuilder.CreateIndex(
                name: "IX_InvoicePromotions_AppliedBy",
                schema: "dbo",
                table: "InvoicePromotions",
                column: "AppliedBy");

            migrationBuilder.CreateIndex(
                name: "IX_InvoicePromotions_InvoiceID",
                schema: "dbo",
                table: "InvoicePromotions",
                column: "InvoiceID");

            migrationBuilder.CreateIndex(
                name: "IX_InvoicePromotions_PromotionID",
                schema: "dbo",
                table: "InvoicePromotions",
                column: "PromotionID");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Barcode",
                schema: "dbo",
                table: "Products",
                column: "Barcode",
                unique: true,
                filter: "[Barcode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Products_BaseUnitID",
                schema: "dbo",
                table: "Products",
                column: "BaseUnitID");

            migrationBuilder.CreateIndex(
                name: "IX_Products_CategoryID",
                schema: "dbo",
                table: "Products",
                column: "CategoryID");

            migrationBuilder.CreateIndex(
                name: "IX_Products_CompanyID",
                schema: "dbo",
                table: "Products",
                column: "CompanyID");

            migrationBuilder.CreateIndex(
                name: "IX_Products_CreatedBy",
                schema: "dbo",
                table: "Products",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Products_SKU",
                schema: "dbo",
                table: "Products",
                column: "SKU",
                unique: true,
                filter: "[SKU] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Products_UpdatedBy",
                schema: "dbo",
                table: "Products",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ProductUnits_CreatedBy",
                schema: "dbo",
                table: "ProductUnits",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ProductUnits_ProductID",
                schema: "dbo",
                table: "ProductUnits",
                column: "ProductID");

            migrationBuilder.CreateIndex(
                name: "IX_ProductUnits_UnitID",
                schema: "dbo",
                table: "ProductUnits",
                column: "UnitID");

            migrationBuilder.CreateIndex(
                name: "IX_ProductUnits_UpdatedBy",
                schema: "dbo",
                table: "ProductUnits",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_PromotionCampaigns_CompanyID",
                schema: "dbo",
                table: "PromotionCampaigns",
                column: "CompanyID");

            migrationBuilder.CreateIndex(
                name: "IX_PromotionCampaigns_CreatedBy",
                schema: "dbo",
                table: "PromotionCampaigns",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_PromotionRules_BuyProductID",
                schema: "dbo",
                table: "PromotionRules",
                column: "BuyProductID");

            migrationBuilder.CreateIndex(
                name: "IX_PromotionRules_FreeProductID",
                schema: "dbo",
                table: "PromotionRules",
                column: "FreeProductID");

            migrationBuilder.CreateIndex(
                name: "IX_PromotionRules_PromotionID",
                schema: "dbo",
                table: "PromotionRules",
                column: "PromotionID");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoiceItems_CreatedBy",
                schema: "dbo",
                table: "PurchaseInvoiceItems",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoiceItems_ProductID",
                schema: "dbo",
                table: "PurchaseInvoiceItems",
                column: "ProductID");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoiceItems_ProductUnitID",
                schema: "dbo",
                table: "PurchaseInvoiceItems",
                column: "ProductUnitID");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoiceItems_PurchaseInvoiceID",
                schema: "dbo",
                table: "PurchaseInvoiceItems",
                column: "PurchaseInvoiceID");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoiceItems_UpdatedBy",
                schema: "dbo",
                table: "PurchaseInvoiceItems",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoices_CompanyID",
                schema: "dbo",
                table: "PurchaseInvoices",
                column: "CompanyID");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoices_CreatedBy",
                schema: "dbo",
                table: "PurchaseInvoices",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoices_InvoiceNumber",
                schema: "dbo",
                table: "PurchaseInvoices",
                column: "InvoiceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoices_UpdatedBy",
                schema: "dbo",
                table: "PurchaseInvoices",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoices_WarehouseID",
                schema: "dbo",
                table: "PurchaseInvoices",
                column: "WarehouseID");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturnItems_ProductID",
                schema: "dbo",
                table: "PurchaseReturnItems",
                column: "ProductID");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturnItems_ProductUnitID",
                schema: "dbo",
                table: "PurchaseReturnItems",
                column: "ProductUnitID");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturnItems_PurchaseInvoiceItemID",
                schema: "dbo",
                table: "PurchaseReturnItems",
                column: "PurchaseInvoiceItemID");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturnItems_PurchaseReturnID",
                schema: "dbo",
                table: "PurchaseReturnItems",
                column: "PurchaseReturnID");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturns_CompanyID",
                schema: "dbo",
                table: "PurchaseReturns",
                column: "CompanyID");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturns_CreatedBy",
                schema: "dbo",
                table: "PurchaseReturns",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturns_PurchaseInvoiceID",
                schema: "dbo",
                table: "PurchaseReturns",
                column: "PurchaseInvoiceID");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturns_WarehouseID",
                schema: "dbo",
                table: "PurchaseReturns",
                column: "WarehouseID");

            migrationBuilder.CreateIndex(
                name: "IX_QuotationItems_CreatedBy",
                schema: "dbo",
                table: "QuotationItems",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_QuotationItems_ProductID",
                schema: "dbo",
                table: "QuotationItems",
                column: "ProductID");

            migrationBuilder.CreateIndex(
                name: "IX_QuotationItems_ProductUnitID",
                schema: "dbo",
                table: "QuotationItems",
                column: "ProductUnitID");

            migrationBuilder.CreateIndex(
                name: "IX_QuotationItems_QuotationID",
                schema: "dbo",
                table: "QuotationItems",
                column: "QuotationID");

            migrationBuilder.CreateIndex(
                name: "IX_QuotationItems_UpdatedBy",
                schema: "dbo",
                table: "QuotationItems",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Quotations_CreatedBy",
                schema: "dbo",
                table: "Quotations",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Quotations_CustomerID",
                schema: "dbo",
                table: "Quotations",
                column: "CustomerID");

            migrationBuilder.CreateIndex(
                name: "IX_Quotations_UpdatedBy",
                schema: "dbo",
                table: "Quotations",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_ApplicationPageID",
                schema: "dbo",
                table: "RolePermissions",
                column: "ApplicationPageID");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_RoleID_ApplicationPageID",
                schema: "dbo",
                table: "RolePermissions",
                columns: new[] { "RoleID", "ApplicationPageID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Roles_CreatedBy",
                schema: "dbo",
                table: "Roles",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_RoleName",
                schema: "dbo",
                table: "Roles",
                column: "RoleName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Roles_UpdatedBy",
                schema: "dbo",
                table: "Roles",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoiceItems_CreatedBy",
                schema: "dbo",
                table: "SalesInvoiceItems",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoiceItems_InvoiceID",
                schema: "dbo",
                table: "SalesInvoiceItems",
                column: "InvoiceID");

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoiceItems_ProductID",
                schema: "dbo",
                table: "SalesInvoiceItems",
                column: "ProductID");

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoiceItems_ProductUnitID",
                schema: "dbo",
                table: "SalesInvoiceItems",
                column: "ProductUnitID");

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoiceItems_PromotionID",
                schema: "dbo",
                table: "SalesInvoiceItems",
                column: "PromotionID");

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoiceItems_UpdatedBy",
                schema: "dbo",
                table: "SalesInvoiceItems",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoices_AreaID",
                schema: "dbo",
                table: "SalesInvoices",
                column: "AreaID");

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoices_BookerID",
                schema: "dbo",
                table: "SalesInvoices",
                column: "BookerID");

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoices_CompanyID",
                schema: "dbo",
                table: "SalesInvoices",
                column: "CompanyID");

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoices_CreatedBy",
                schema: "dbo",
                table: "SalesInvoices",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoices_CustomerID",
                schema: "dbo",
                table: "SalesInvoices",
                column: "CustomerID");

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoices_InvoiceNumber",
                schema: "dbo",
                table: "SalesInvoices",
                column: "InvoiceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoices_QuotationID",
                schema: "dbo",
                table: "SalesInvoices",
                column: "QuotationID");

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoices_SalespersonID",
                schema: "dbo",
                table: "SalesInvoices",
                column: "SalespersonID");

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoices_SubAreaID",
                schema: "dbo",
                table: "SalesInvoices",
                column: "SubAreaID");

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoices_SupplierID",
                schema: "dbo",
                table: "SalesInvoices",
                column: "SupplierID");

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoices_UpdatedBy",
                schema: "dbo",
                table: "SalesInvoices",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoices_WarehouseID",
                schema: "dbo",
                table: "SalesInvoices",
                column: "WarehouseID");

            migrationBuilder.CreateIndex(
                name: "IX_SalesReturnItems_InvoiceItemID",
                schema: "dbo",
                table: "SalesReturnItems",
                column: "InvoiceItemID");

            migrationBuilder.CreateIndex(
                name: "IX_SalesReturnItems_ProductID",
                schema: "dbo",
                table: "SalesReturnItems",
                column: "ProductID");

            migrationBuilder.CreateIndex(
                name: "IX_SalesReturnItems_ProductUnitID",
                schema: "dbo",
                table: "SalesReturnItems",
                column: "ProductUnitID");

            migrationBuilder.CreateIndex(
                name: "IX_SalesReturnItems_SalesReturnID",
                schema: "dbo",
                table: "SalesReturnItems",
                column: "SalesReturnID");

            migrationBuilder.CreateIndex(
                name: "IX_SalesReturns_CreatedBy",
                schema: "dbo",
                table: "SalesReturns",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_SalesReturns_CustomerID",
                schema: "dbo",
                table: "SalesReturns",
                column: "CustomerID");

            migrationBuilder.CreateIndex(
                name: "IX_SalesReturns_InvoiceID",
                schema: "dbo",
                table: "SalesReturns",
                column: "InvoiceID");

            migrationBuilder.CreateIndex(
                name: "IX_SalesReturns_WarehouseID",
                schema: "dbo",
                table: "SalesReturns",
                column: "WarehouseID");

            migrationBuilder.CreateIndex(
                name: "IX_SubAreas_AreaID",
                schema: "dbo",
                table: "SubAreas",
                column: "AreaID");

            migrationBuilder.CreateIndex(
                name: "IX_SubAreas_CreatedBy",
                schema: "dbo",
                table: "SubAreas",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_SubAreas_UpdatedBy",
                schema: "dbo",
                table: "SubAreas",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_CreatedBy",
                schema: "dbo",
                table: "Suppliers",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_UpdatedBy",
                schema: "dbo",
                table: "Suppliers",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Units_CreatedBy",
                schema: "dbo",
                table: "Units",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Units_UnitName",
                schema: "dbo",
                table: "Units",
                column: "UnitName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Units_UpdatedBy",
                schema: "dbo",
                table: "Units",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_UserCompanies_CompanyID",
                schema: "dbo",
                table: "UserCompanies",
                column: "CompanyID");

            migrationBuilder.CreateIndex(
                name: "IX_UserCompanies_CreatedBy",
                schema: "dbo",
                table: "UserCompanies",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_UserCompanies_UpdatedBy",
                schema: "dbo",
                table: "UserCompanies",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_UserCompanies_UserID_CompanyID",
                schema: "dbo",
                table: "UserCompanies",
                columns: new[] { "UserID", "CompanyID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_CreatedBy",
                schema: "dbo",
                table: "Users",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Users_RoleID",
                schema: "dbo",
                table: "Users",
                column: "RoleID");

            migrationBuilder.CreateIndex(
                name: "IX_Users_UpdatedBy",
                schema: "dbo",
                table: "Users",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                schema: "dbo",
                table: "Users",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_CreatedBy",
                schema: "dbo",
                table: "Warehouses",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_UpdatedBy",
                schema: "dbo",
                table: "Warehouses",
                column: "UpdatedBy");

            migrationBuilder.AddForeignKey(
                name: "FK_Areas_Users_CreatedBy",
                schema: "dbo",
                table: "Areas",
                column: "CreatedBy",
                principalSchema: "dbo",
                principalTable: "Users",
                principalColumn: "UserID");

            migrationBuilder.AddForeignKey(
                name: "FK_Areas_Users_UpdatedBy",
                schema: "dbo",
                table: "Areas",
                column: "UpdatedBy",
                principalSchema: "dbo",
                principalTable: "Users",
                principalColumn: "UserID");

            migrationBuilder.AddForeignKey(
                name: "FK_AuditLogs_Users_UserID",
                schema: "dbo",
                table: "AuditLogs",
                column: "UserID",
                principalSchema: "dbo",
                principalTable: "Users",
                principalColumn: "UserID");

            migrationBuilder.AddForeignKey(
                name: "FK_Bookers_Companies_CompanyID",
                schema: "dbo",
                table: "Bookers",
                column: "CompanyID",
                principalSchema: "dbo",
                principalTable: "Companies",
                principalColumn: "CompanyID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Bookers_Users_CreatedBy",
                schema: "dbo",
                table: "Bookers",
                column: "CreatedBy",
                principalSchema: "dbo",
                principalTable: "Users",
                principalColumn: "UserID");

            migrationBuilder.AddForeignKey(
                name: "FK_Bookers_Users_UpdatedBy",
                schema: "dbo",
                table: "Bookers",
                column: "UpdatedBy",
                principalSchema: "dbo",
                principalTable: "Users",
                principalColumn: "UserID");

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_Companies_CompanyID",
                schema: "dbo",
                table: "Categories",
                column: "CompanyID",
                principalSchema: "dbo",
                principalTable: "Companies",
                principalColumn: "CompanyID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_Users_CreatedBy",
                schema: "dbo",
                table: "Categories",
                column: "CreatedBy",
                principalSchema: "dbo",
                principalTable: "Users",
                principalColumn: "UserID");

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_Users_UpdatedBy",
                schema: "dbo",
                table: "Categories",
                column: "UpdatedBy",
                principalSchema: "dbo",
                principalTable: "Users",
                principalColumn: "UserID");

            migrationBuilder.AddForeignKey(
                name: "FK_ChequeStatusAudits_Users_UpdatedBy",
                schema: "dbo",
                table: "ChequeStatusAudits",
                column: "UpdatedBy",
                principalSchema: "dbo",
                principalTable: "Users",
                principalColumn: "UserID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Companies_Users_UpdatedBy",
                schema: "dbo",
                table: "Companies",
                column: "UpdatedBy",
                principalSchema: "dbo",
                principalTable: "Users",
                principalColumn: "UserID");

            migrationBuilder.AddForeignKey(
                name: "FK_CompanyLedger_CompanyPayments_CompanyPaymentID",
                schema: "dbo",
                table: "CompanyLedger",
                column: "CompanyPaymentID",
                principalSchema: "dbo",
                principalTable: "CompanyPayments",
                principalColumn: "CompanyPaymentID");

            migrationBuilder.AddForeignKey(
                name: "FK_CompanyLedger_PurchaseInvoices_PurchaseInvoiceID",
                schema: "dbo",
                table: "CompanyLedger",
                column: "PurchaseInvoiceID",
                principalSchema: "dbo",
                principalTable: "PurchaseInvoices",
                principalColumn: "PurchaseInvoiceID");

            migrationBuilder.AddForeignKey(
                name: "FK_CompanyLedger_PurchaseReturns_PurchaseReturnID",
                schema: "dbo",
                table: "CompanyLedger",
                column: "PurchaseReturnID",
                principalSchema: "dbo",
                principalTable: "PurchaseReturns",
                principalColumn: "PurchaseReturnID");

            migrationBuilder.AddForeignKey(
                name: "FK_CompanyLedger_Users_CreatedBy",
                schema: "dbo",
                table: "CompanyLedger",
                column: "CreatedBy",
                principalSchema: "dbo",
                principalTable: "Users",
                principalColumn: "UserID");

            migrationBuilder.AddForeignKey(
                name: "FK_CompanyPayments_PurchaseInvoices_PurchaseInvoiceID",
                schema: "dbo",
                table: "CompanyPayments",
                column: "PurchaseInvoiceID",
                principalSchema: "dbo",
                principalTable: "PurchaseInvoices",
                principalColumn: "PurchaseInvoiceID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CompanyPayments_Users_PaidBy",
                schema: "dbo",
                table: "CompanyPayments",
                column: "PaidBy",
                principalSchema: "dbo",
                principalTable: "Users",
                principalColumn: "UserID");

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerLedger_CustomerPayments_CustomerPaymentID",
                schema: "dbo",
                table: "CustomerLedger",
                column: "CustomerPaymentID",
                principalSchema: "dbo",
                principalTable: "CustomerPayments",
                principalColumn: "CustomerPaymentID");

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerLedger_Customers_CustomerID",
                schema: "dbo",
                table: "CustomerLedger",
                column: "CustomerID",
                principalSchema: "dbo",
                principalTable: "Customers",
                principalColumn: "CustomerID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerLedger_SalesInvoices_SalesInvoiceID",
                schema: "dbo",
                table: "CustomerLedger",
                column: "SalesInvoiceID",
                principalSchema: "dbo",
                principalTable: "SalesInvoices",
                principalColumn: "InvoiceID");

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerLedger_SalesReturns_SalesReturnID",
                schema: "dbo",
                table: "CustomerLedger",
                column: "SalesReturnID",
                principalSchema: "dbo",
                principalTable: "SalesReturns",
                principalColumn: "SalesReturnID");

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerLedger_Users_CreatedBy",
                schema: "dbo",
                table: "CustomerLedger",
                column: "CreatedBy",
                principalSchema: "dbo",
                principalTable: "Users",
                principalColumn: "UserID");

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerPayments_Customers_CustomerID",
                schema: "dbo",
                table: "CustomerPayments",
                column: "CustomerID",
                principalSchema: "dbo",
                principalTable: "Customers",
                principalColumn: "CustomerID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerPayments_SalesInvoices_InvoiceID",
                schema: "dbo",
                table: "CustomerPayments",
                column: "InvoiceID",
                principalSchema: "dbo",
                principalTable: "SalesInvoices",
                principalColumn: "InvoiceID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerPayments_Users_ReceivedBy",
                schema: "dbo",
                table: "CustomerPayments",
                column: "ReceivedBy",
                principalSchema: "dbo",
                principalTable: "Users",
                principalColumn: "UserID");

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_SubAreas_SubAreaID",
                schema: "dbo",
                table: "Customers",
                column: "SubAreaID",
                principalSchema: "dbo",
                principalTable: "SubAreas",
                principalColumn: "SubAreaID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_Users_CreatedBy",
                schema: "dbo",
                table: "Customers",
                column: "CreatedBy",
                principalSchema: "dbo",
                principalTable: "Users",
                principalColumn: "UserID");

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_Users_UpdatedBy",
                schema: "dbo",
                table: "Customers",
                column: "UpdatedBy",
                principalSchema: "dbo",
                principalTable: "Users",
                principalColumn: "UserID");

            migrationBuilder.AddForeignKey(
                name: "FK_DiscountRules_Users_CreatedBy",
                schema: "dbo",
                table: "DiscountRules",
                column: "CreatedBy",
                principalSchema: "dbo",
                principalTable: "Users",
                principalColumn: "UserID");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryCostConsumptions_InventoryCostLayers_CostLayerID",
                schema: "dbo",
                table: "InventoryCostConsumptions",
                column: "CostLayerID",
                principalSchema: "dbo",
                principalTable: "InventoryCostLayers",
                principalColumn: "CostLayerID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryCostConsumptions_SalesInvoiceItems_SalesInvoiceItemID",
                schema: "dbo",
                table: "InventoryCostConsumptions",
                column: "SalesInvoiceItemID",
                principalSchema: "dbo",
                principalTable: "SalesInvoiceItems",
                principalColumn: "InvoiceItemID");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryCostConsumptions_Users_CreatedBy",
                schema: "dbo",
                table: "InventoryCostConsumptions",
                column: "CreatedBy",
                principalSchema: "dbo",
                principalTable: "Users",
                principalColumn: "UserID");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryCostLayers_Products_ProductID",
                schema: "dbo",
                table: "InventoryCostLayers",
                column: "ProductID",
                principalSchema: "dbo",
                principalTable: "Products",
                principalColumn: "ProductID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryCostLayers_PurchaseInvoiceItems_PurchaseInvoiceItemID",
                schema: "dbo",
                table: "InventoryCostLayers",
                column: "PurchaseInvoiceItemID",
                principalSchema: "dbo",
                principalTable: "PurchaseInvoiceItems",
                principalColumn: "PurchaseItemID");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryCostLayers_SalesReturnItems_SalesReturnItemID",
                schema: "dbo",
                table: "InventoryCostLayers",
                column: "SalesReturnItemID",
                principalSchema: "dbo",
                principalTable: "SalesReturnItems",
                principalColumn: "SalesReturnItemID");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryCostLayers_Users_CreatedBy",
                schema: "dbo",
                table: "InventoryCostLayers",
                column: "CreatedBy",
                principalSchema: "dbo",
                principalTable: "Users",
                principalColumn: "UserID");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryCostLayers_Warehouses_WarehouseID",
                schema: "dbo",
                table: "InventoryCostLayers",
                column: "WarehouseID",
                principalSchema: "dbo",
                principalTable: "Warehouses",
                principalColumn: "WarehouseID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryStock_Products_ProductID",
                schema: "dbo",
                table: "InventoryStock",
                column: "ProductID",
                principalSchema: "dbo",
                principalTable: "Products",
                principalColumn: "ProductID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryStock_Users_CreatedBy",
                schema: "dbo",
                table: "InventoryStock",
                column: "CreatedBy",
                principalSchema: "dbo",
                principalTable: "Users",
                principalColumn: "UserID");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryStock_Users_UpdatedBy",
                schema: "dbo",
                table: "InventoryStock",
                column: "UpdatedBy",
                principalSchema: "dbo",
                principalTable: "Users",
                principalColumn: "UserID");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryStock_Warehouses_WarehouseID",
                schema: "dbo",
                table: "InventoryStock",
                column: "WarehouseID",
                principalSchema: "dbo",
                principalTable: "Warehouses",
                principalColumn: "WarehouseID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryTransactions_Products_ProductID",
                schema: "dbo",
                table: "InventoryTransactions",
                column: "ProductID",
                principalSchema: "dbo",
                principalTable: "Products",
                principalColumn: "ProductID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryTransactions_PurchaseInvoiceItems_PurchaseInvoiceItemID",
                schema: "dbo",
                table: "InventoryTransactions",
                column: "PurchaseInvoiceItemID",
                principalSchema: "dbo",
                principalTable: "PurchaseInvoiceItems",
                principalColumn: "PurchaseItemID");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryTransactions_PurchaseReturnItems_PurchaseReturnItemID",
                schema: "dbo",
                table: "InventoryTransactions",
                column: "PurchaseReturnItemID",
                principalSchema: "dbo",
                principalTable: "PurchaseReturnItems",
                principalColumn: "PurchaseReturnItemID");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryTransactions_SalesInvoiceItems_SalesInvoiceItemID",
                schema: "dbo",
                table: "InventoryTransactions",
                column: "SalesInvoiceItemID",
                principalSchema: "dbo",
                principalTable: "SalesInvoiceItems",
                principalColumn: "InvoiceItemID");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryTransactions_SalesReturnItems_SalesReturnItemID",
                schema: "dbo",
                table: "InventoryTransactions",
                column: "SalesReturnItemID",
                principalSchema: "dbo",
                principalTable: "SalesReturnItems",
                principalColumn: "SalesReturnItemID");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryTransactions_Users_CreatedBy",
                schema: "dbo",
                table: "InventoryTransactions",
                column: "CreatedBy",
                principalSchema: "dbo",
                principalTable: "Users",
                principalColumn: "UserID");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryTransactions_Users_UpdatedBy",
                schema: "dbo",
                table: "InventoryTransactions",
                column: "UpdatedBy",
                principalSchema: "dbo",
                principalTable: "Users",
                principalColumn: "UserID");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryTransactions_Warehouses_WarehouseID",
                schema: "dbo",
                table: "InventoryTransactions",
                column: "WarehouseID",
                principalSchema: "dbo",
                principalTable: "Warehouses",
                principalColumn: "WarehouseID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InvoiceDiscounts_SalesInvoices_InvoiceID",
                schema: "dbo",
                table: "InvoiceDiscounts",
                column: "InvoiceID",
                principalSchema: "dbo",
                principalTable: "SalesInvoices",
                principalColumn: "InvoiceID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InvoiceDiscounts_Users_AppliedBy",
                schema: "dbo",
                table: "InvoiceDiscounts",
                column: "AppliedBy",
                principalSchema: "dbo",
                principalTable: "Users",
                principalColumn: "UserID");

            migrationBuilder.AddForeignKey(
                name: "FK_InvoiceEditAudits_Users_EditedBy",
                schema: "dbo",
                table: "InvoiceEditAudits",
                column: "EditedBy",
                principalSchema: "dbo",
                principalTable: "Users",
                principalColumn: "UserID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_InvoicePromotions_PromotionCampaigns_PromotionID",
                schema: "dbo",
                table: "InvoicePromotions",
                column: "PromotionID",
                principalSchema: "dbo",
                principalTable: "PromotionCampaigns",
                principalColumn: "PromotionID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InvoicePromotions_SalesInvoices_InvoiceID",
                schema: "dbo",
                table: "InvoicePromotions",
                column: "InvoiceID",
                principalSchema: "dbo",
                principalTable: "SalesInvoices",
                principalColumn: "InvoiceID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InvoicePromotions_Users_AppliedBy",
                schema: "dbo",
                table: "InvoicePromotions",
                column: "AppliedBy",
                principalSchema: "dbo",
                principalTable: "Users",
                principalColumn: "UserID");

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Units_BaseUnitID",
                schema: "dbo",
                table: "Products",
                column: "BaseUnitID",
                principalSchema: "dbo",
                principalTable: "Units",
                principalColumn: "UnitID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Users_CreatedBy",
                schema: "dbo",
                table: "Products",
                column: "CreatedBy",
                principalSchema: "dbo",
                principalTable: "Users",
                principalColumn: "UserID");

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Users_UpdatedBy",
                schema: "dbo",
                table: "Products",
                column: "UpdatedBy",
                principalSchema: "dbo",
                principalTable: "Users",
                principalColumn: "UserID");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductUnits_Units_UnitID",
                schema: "dbo",
                table: "ProductUnits",
                column: "UnitID",
                principalSchema: "dbo",
                principalTable: "Units",
                principalColumn: "UnitID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductUnits_Users_CreatedBy",
                schema: "dbo",
                table: "ProductUnits",
                column: "CreatedBy",
                principalSchema: "dbo",
                principalTable: "Users",
                principalColumn: "UserID");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductUnits_Users_UpdatedBy",
                schema: "dbo",
                table: "ProductUnits",
                column: "UpdatedBy",
                principalSchema: "dbo",
                principalTable: "Users",
                principalColumn: "UserID");

            migrationBuilder.AddForeignKey(
                name: "FK_PromotionCampaigns_Users_CreatedBy",
                schema: "dbo",
                table: "PromotionCampaigns",
                column: "CreatedBy",
                principalSchema: "dbo",
                principalTable: "Users",
                principalColumn: "UserID");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseInvoiceItems_PurchaseInvoices_PurchaseInvoiceID",
                schema: "dbo",
                table: "PurchaseInvoiceItems",
                column: "PurchaseInvoiceID",
                principalSchema: "dbo",
                principalTable: "PurchaseInvoices",
                principalColumn: "PurchaseInvoiceID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseInvoiceItems_Users_CreatedBy",
                schema: "dbo",
                table: "PurchaseInvoiceItems",
                column: "CreatedBy",
                principalSchema: "dbo",
                principalTable: "Users",
                principalColumn: "UserID");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseInvoiceItems_Users_UpdatedBy",
                schema: "dbo",
                table: "PurchaseInvoiceItems",
                column: "UpdatedBy",
                principalSchema: "dbo",
                principalTable: "Users",
                principalColumn: "UserID");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseInvoices_Users_CreatedBy",
                schema: "dbo",
                table: "PurchaseInvoices",
                column: "CreatedBy",
                principalSchema: "dbo",
                principalTable: "Users",
                principalColumn: "UserID");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseInvoices_Users_UpdatedBy",
                schema: "dbo",
                table: "PurchaseInvoices",
                column: "UpdatedBy",
                principalSchema: "dbo",
                principalTable: "Users",
                principalColumn: "UserID");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseInvoices_Warehouses_WarehouseID",
                schema: "dbo",
                table: "PurchaseInvoices",
                column: "WarehouseID",
                principalSchema: "dbo",
                principalTable: "Warehouses",
                principalColumn: "WarehouseID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseReturnItems_PurchaseReturns_PurchaseReturnID",
                schema: "dbo",
                table: "PurchaseReturnItems",
                column: "PurchaseReturnID",
                principalSchema: "dbo",
                principalTable: "PurchaseReturns",
                principalColumn: "PurchaseReturnID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseReturns_Users_CreatedBy",
                schema: "dbo",
                table: "PurchaseReturns",
                column: "CreatedBy",
                principalSchema: "dbo",
                principalTable: "Users",
                principalColumn: "UserID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseReturns_Warehouses_WarehouseID",
                schema: "dbo",
                table: "PurchaseReturns",
                column: "WarehouseID",
                principalSchema: "dbo",
                principalTable: "Warehouses",
                principalColumn: "WarehouseID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_QuotationItems_Quotations_QuotationID",
                schema: "dbo",
                table: "QuotationItems",
                column: "QuotationID",
                principalSchema: "dbo",
                principalTable: "Quotations",
                principalColumn: "QuotationID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_QuotationItems_Users_CreatedBy",
                schema: "dbo",
                table: "QuotationItems",
                column: "CreatedBy",
                principalSchema: "dbo",
                principalTable: "Users",
                principalColumn: "UserID");

            migrationBuilder.AddForeignKey(
                name: "FK_QuotationItems_Users_UpdatedBy",
                schema: "dbo",
                table: "QuotationItems",
                column: "UpdatedBy",
                principalSchema: "dbo",
                principalTable: "Users",
                principalColumn: "UserID");

            migrationBuilder.AddForeignKey(
                name: "FK_Quotations_Users_CreatedBy",
                schema: "dbo",
                table: "Quotations",
                column: "CreatedBy",
                principalSchema: "dbo",
                principalTable: "Users",
                principalColumn: "UserID");

            migrationBuilder.AddForeignKey(
                name: "FK_Quotations_Users_UpdatedBy",
                schema: "dbo",
                table: "Quotations",
                column: "UpdatedBy",
                principalSchema: "dbo",
                principalTable: "Users",
                principalColumn: "UserID");

            migrationBuilder.AddForeignKey(
                name: "FK_RolePermissions_Roles_RoleID",
                schema: "dbo",
                table: "RolePermissions",
                column: "RoleID",
                principalSchema: "dbo",
                principalTable: "Roles",
                principalColumn: "RoleID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Roles_Users_CreatedBy",
                schema: "dbo",
                table: "Roles",
                column: "CreatedBy",
                principalSchema: "dbo",
                principalTable: "Users",
                principalColumn: "UserID");

            migrationBuilder.AddForeignKey(
                name: "FK_Roles_Users_UpdatedBy",
                schema: "dbo",
                table: "Roles",
                column: "UpdatedBy",
                principalSchema: "dbo",
                principalTable: "Users",
                principalColumn: "UserID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Roles_Users_CreatedBy",
                schema: "dbo",
                table: "Roles");

            migrationBuilder.DropForeignKey(
                name: "FK_Roles_Users_UpdatedBy",
                schema: "dbo",
                table: "Roles");

            migrationBuilder.DropTable(
                name: "AuditLogs",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ChequeStatusAudits",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "CompanyLedger",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "CustomerLedger",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "InventoryCostConsumptions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "InventoryStock",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "InventoryTransactions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "InvoiceDiscounts",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "InvoiceEditAudits",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "InvoicePromotions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PromotionRules",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "QuotationItems",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RolePermissions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "UserCompanies",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "CompanyPayments",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "CustomerPayments",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "InventoryCostLayers",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PurchaseReturnItems",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "DiscountRules",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ApplicationPages",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "SalesReturnItems",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PurchaseInvoiceItems",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PurchaseReturns",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ApplicationModules",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "SalesInvoiceItems",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "SalesReturns",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PurchaseInvoices",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ProductUnits",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PromotionCampaigns",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "SalesInvoices",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Products",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Bookers",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Quotations",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Suppliers",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Warehouses",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Categories",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Units",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Customers",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Companies",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "SubAreas",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Areas",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Users",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Roles",
                schema: "dbo");
        }
    }
}
