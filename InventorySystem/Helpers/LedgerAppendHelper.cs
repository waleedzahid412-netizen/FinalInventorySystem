using System;
using System.Collections.Generic;
using InventorySystem.Models.Entities;

namespace InventorySystem.Helpers
{
    /// <summary>
    /// Append-only ledger corrections for invoice edits (BR-002 / BR-041).
    /// Original SALE/PURCHASE rows are never mutated; reversals and new amounts are appended.
    /// </summary>
    public static class LedgerAppendHelper
    {
        public static void AppendSalesInvoiceCorrection(
            SalesInvoice invoice,
            decimal oldGrandTotal,
            decimal newGrandTotal,
            DateTime transactionDate,
            int userId,
            DateTime now,
            ICollection<CustomerLedger> ledgerEntries)
        {
            if (oldGrandTotal == newGrandTotal)
            {
                return;
            }

            ledgerEntries.Add(new CustomerLedger
            {
                CustomerID = invoice.CustomerID,
                TransactionDate = transactionDate,
                TransactionType = "ADJUSTMENT",
                DebitAmount = 0m,
                CreditAmount = oldGrandTotal,
                SalesInvoiceID = invoice.InvoiceID,
                Description = $"Invoice edit reversal #{invoice.InvoiceNumber} (v{invoice.Version})",
                CreatedBy = userId,
                CreatedAt = now
            });

            ledgerEntries.Add(new CustomerLedger
            {
                CustomerID = invoice.CustomerID,
                TransactionDate = transactionDate,
                TransactionType = "SALE",
                DebitAmount = newGrandTotal,
                CreditAmount = 0m,
                SalesInvoiceID = invoice.InvoiceID,
                Description = $"Invoice edit corrected amount #{invoice.InvoiceNumber} (v{invoice.Version})",
                CreatedBy = userId,
                CreatedAt = now
            });
        }

        public static void AppendPurchaseInvoiceCorrection(
            PurchaseInvoice invoice,
            decimal oldGrandTotal,
            decimal newGrandTotal,
            DateTime transactionDate,
            int userId,
            DateTime now,
            ICollection<CompanyLedger> ledgerEntries)
        {
            if (oldGrandTotal == newGrandTotal)
            {
                return;
            }

            ledgerEntries.Add(new CompanyLedger
            {
                CompanyID = invoice.CompanyID,
                TransactionDate = transactionDate,
                TransactionType = "ADJUSTMENT",
                DebitAmount = oldGrandTotal,
                CreditAmount = 0m,
                PurchaseInvoiceID = invoice.PurchaseInvoiceID,
                Description = $"Purchase edit reversal #{invoice.InvoiceNumber} (v{invoice.Version})",
                CreatedBy = userId,
                CreatedAt = now
            });

            ledgerEntries.Add(new CompanyLedger
            {
                CompanyID = invoice.CompanyID,
                TransactionDate = transactionDate,
                TransactionType = "PURCHASE",
                DebitAmount = 0m,
                CreditAmount = newGrandTotal,
                PurchaseInvoiceID = invoice.PurchaseInvoiceID,
                Description = $"Purchase edit corrected amount #{invoice.InvoiceNumber} (v{invoice.Version})",
                CreatedBy = userId,
                CreatedAt = now
            });
        }
    }
}
