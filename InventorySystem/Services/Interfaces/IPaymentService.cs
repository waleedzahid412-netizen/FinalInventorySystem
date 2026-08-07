using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using InventorySystem.DTOs.Common;
using InventorySystem.DTOs.Payments;

namespace InventorySystem.Services.Interfaces
{
    public interface IPaymentService
    {
        Task<CustomerPaymentDto> RecordCustomerPaymentAsync(RecordCustomerPaymentRequest request, int userId, CancellationToken cancellationToken = default);
        Task<CompanyPaymentDto> RecordCompanyPaymentAsync(RecordCompanyPaymentRequest request, int userId, CancellationToken cancellationToken = default);

        Task<decimal> GetCustomerOutstandingBalanceAsync(int customerId, CancellationToken cancellationToken = default);
        Task<decimal> GetCompanyOutstandingBalanceAsync(int companyId, CancellationToken cancellationToken = default);

        Task<PagedResult<CustomerPaymentDto>> GetPagedCustomerPaymentsAsync(PaymentFilterDto filter, CancellationToken cancellationToken = default);
        Task<PagedResult<CompanyPaymentDto>> GetPagedCompanyPaymentsAsync(PaymentFilterDto filter, CancellationToken cancellationToken = default);

        Task<List<CustomerPaymentDto>> GetPaymentsBySalesInvoiceAsync(int salesInvoiceId, CancellationToken cancellationToken = default);
        Task<List<CompanyPaymentDto>> GetPaymentsByPurchaseInvoiceAsync(int purchaseInvoiceId, CancellationToken cancellationToken = default);

        Task<CustomerPaymentDto?> GetCustomerPaymentByIdAsync(int paymentId, CancellationToken cancellationToken = default);
        Task<CompanyPaymentDto?> GetCompanyPaymentByIdAsync(int paymentId, CancellationToken cancellationToken = default);

        Task<IEnumerable<CustomerStatementEntryDto>> GetCustomerStatementAsync(int customerId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
        Task<IEnumerable<CompanyStatementEntryDto>> GetCompanyStatementAsync(int companyId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

        Task<List<UnpaidInvoiceLookupDto>> GetUnpaidCustomerInvoicesAsync(int customerId, CancellationToken cancellationToken = default);
        Task<List<UnpaidInvoiceLookupDto>> GetUnpaidCompanyInvoicesAsync(int companyId, CancellationToken cancellationToken = default);

        // ===== CHEQUE LIFECYCLE MANAGEMENT =====
        Task<OperationResult> ClearCustomerChequeAsync(int customerPaymentId, int userId, CancellationToken cancellationToken = default);
        Task<OperationResult> BounceCustomerChequeAsync(int customerPaymentId, string remarks, int userId, CancellationToken cancellationToken = default);
        Task<OperationResult> CancelCustomerChequeAsync(int customerPaymentId, string remarks, int userId, CancellationToken cancellationToken = default);

        Task<OperationResult> ClearCompanyChequeAsync(int companyPaymentId, int userId, CancellationToken cancellationToken = default);
        Task<OperationResult> BounceCompanyChequeAsync(int companyPaymentId, string remarks, int userId, CancellationToken cancellationToken = default);
        Task<OperationResult> CancelCompanyChequeAsync(int companyPaymentId, string remarks, int userId, CancellationToken cancellationToken = default);

        Task<PagedResult<ChequeDetailDto>> GetPagedChequesAsync(ChequeFilterDto filter, CancellationToken cancellationToken = default);
    }
}
