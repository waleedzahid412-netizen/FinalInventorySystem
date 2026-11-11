-- ============================================================
-- Product-Level Discount schema changes
-- Run against your WIMS SQL Server database.
-- Safe to re-run (IF NOT EXISTS guards).
-- ============================================================

SET NOCOUNT ON;
GO

-- 1) Products: optional catalog discount
IF COL_LENGTH('dbo.Products', 'ProductDiscountType') IS NULL
BEGIN
    ALTER TABLE dbo.Products ADD ProductDiscountType NVARCHAR(20) NULL;
END
GO

IF COL_LENGTH('dbo.Products', 'ProductDiscountValue') IS NULL
BEGIN
    ALTER TABLE dbo.Products ADD ProductDiscountValue DECIMAL(18,2) NULL;
END
GO

-- 2) SalesInvoices: switch (default ON for new rows)
IF COL_LENGTH('dbo.SalesInvoices', 'ApplyProductDiscount') IS NULL
BEGIN
    ALTER TABLE dbo.SalesInvoices ADD ApplyProductDiscount BIT NOT NULL
        CONSTRAINT DF_SalesInvoices_ApplyProductDiscount DEFAULT (1);
END
GO

-- 3) SalesInvoiceItems: historical product-discount snapshot
IF COL_LENGTH('dbo.SalesInvoiceItems', 'ProductDiscountType') IS NULL
BEGIN
    ALTER TABLE dbo.SalesInvoiceItems ADD ProductDiscountType NVARCHAR(20) NULL;
END
GO

IF COL_LENGTH('dbo.SalesInvoiceItems', 'ProductDiscountValue') IS NULL
BEGIN
    ALTER TABLE dbo.SalesInvoiceItems ADD ProductDiscountValue DECIMAL(18,2) NULL;
END
GO

IF COL_LENGTH('dbo.SalesInvoiceItems', 'ProductDiscountAmount') IS NULL
BEGIN
    ALTER TABLE dbo.SalesInvoiceItems ADD ProductDiscountAmount DECIMAL(18,2) NOT NULL
        CONSTRAINT DF_SalesInvoiceItems_ProductDiscountAmount DEFAULT (0);
END
GO

-- 4) Quotations (schema ready; UI module not built yet)
IF COL_LENGTH('dbo.Quotations', 'ApplyProductDiscount') IS NULL
BEGIN
    ALTER TABLE dbo.Quotations ADD ApplyProductDiscount BIT NOT NULL
        CONSTRAINT DF_Quotations_ApplyProductDiscount DEFAULT (1);
END
GO

IF COL_LENGTH('dbo.QuotationItems', 'ProductDiscountType') IS NULL
BEGIN
    ALTER TABLE dbo.QuotationItems ADD ProductDiscountType NVARCHAR(20) NULL;
END
GO

IF COL_LENGTH('dbo.QuotationItems', 'ProductDiscountValue') IS NULL
BEGIN
    ALTER TABLE dbo.QuotationItems ADD ProductDiscountValue DECIMAL(18,2) NULL;
END
GO

IF COL_LENGTH('dbo.QuotationItems', 'ProductDiscountAmount') IS NULL
BEGIN
    ALTER TABLE dbo.QuotationItems ADD ProductDiscountAmount DECIMAL(18,2) NOT NULL
        CONSTRAINT DF_QuotationItems_ProductDiscountAmount DEFAULT (0);
END
GO

PRINT 'Product-level discount columns applied successfully.';
GO
