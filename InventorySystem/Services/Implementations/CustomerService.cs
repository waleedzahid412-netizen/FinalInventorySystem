using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using InventorySystem.DTOs.Common;
using InventorySystem.DTOs.Customers;
using InventorySystem.Models.Entities;
using InventorySystem.Repositories.Interfaces;
using InventorySystem.Services.Interfaces;

namespace InventorySystem.Services.Implementations
{
    public class CustomerService : ICustomerService
    {
        private readonly ICustomerRepository _customerRepository;
        private readonly ICompanyContext _companyContext;

        public CustomerService(ICustomerRepository customerRepository, ICompanyContext companyContext)
        {
            _customerRepository = customerRepository;
            _companyContext = companyContext;
        }

        private async Task<int?> ResolveSoftCompanyIdAsync(CancellationToken cancellationToken)
        {
            await _companyContext.TryResolveAsync(cancellationToken);
            return _companyContext.HasCompany ? _companyContext.CompanyID : null;
        }

        public async Task<PagedResult<CustomerListDto>> GetPagedCustomersAsync(CustomerFilterDto filter, CancellationToken cancellationToken = default)
        {
            var pagedCustomers = await _customerRepository.GetPagedAsync(filter, cancellationToken);
            int? companyId = await ResolveSoftCompanyIdAsync(cancellationToken);

            var listItems = new List<CustomerListDto>();
            foreach (var c in pagedCustomers.Items)
            {
                var summary = await _customerRepository.GetFinancialSummaryAsync(c.CustomerID, companyId, cancellationToken);
                listItems.Add(new CustomerListDto
                {
                    CustomerID = c.CustomerID,
                    ShopName = c.ShopName,
                    OwnerName = c.OwnerName,
                    Phone = c.Phone,
                    Address = c.Address,
                    AreaName = c.Area?.AreaName,
                    SubAreaName = c.SubArea?.SubAreaName,
                    TaxID = c.TaxID,
                    CreditLimit = c.CreditLimit,
                    IsActive = c.IsActive,
                    OutstandingReceivable = summary?.OutstandingReceivable ?? 0m,
                    CreatedAt = c.CreatedAt
                });
            }

            return new PagedResult<CustomerListDto>(listItems, pagedCustomers.TotalCount, pagedCustomers.PageNumber, pagedCustomers.PageSize);
        }

        public async Task<CustomerInfoDto?> GetCustomerByIdAsync(int customerId, CancellationToken cancellationToken = default)
        {
            var c = await _customerRepository.GetByIdAsync(customerId, cancellationToken);
            if (c == null) return null;

            return new CustomerInfoDto
            {
                CustomerID = c.CustomerID,
                ShopName = c.ShopName,
                OwnerName = c.OwnerName,
                Phone = c.Phone,
                Address = c.Address,
                AreaID = c.AreaID,
                AreaName = c.Area?.AreaName,
                SubAreaID = c.SubAreaID,
                SubAreaName = c.SubArea?.SubAreaName,
                TaxID = c.TaxID,
                CreditLimit = c.CreditLimit,
                PreferredDiscountPercent = c.PreferredDiscountPercent,
                IsActive = c.IsActive,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt
            };
        }

        public async Task<EditCustomerDto?> GetCustomerForEditAsync(int customerId, CancellationToken cancellationToken = default)
        {
            var c = await _customerRepository.GetByIdAsync(customerId, cancellationToken);
            if (c == null) return null;

            return new EditCustomerDto
            {
                CustomerID = c.CustomerID,
                ShopName = c.ShopName,
                OwnerName = c.OwnerName,
                Phone = c.Phone,
                Address = c.Address,
                AreaID = c.AreaID,
                SubAreaID = c.SubAreaID,
                TaxID = c.TaxID,
                CreditLimit = c.CreditLimit,
                PreferredDiscountPercent = c.PreferredDiscountPercent,
                IsActive = c.IsActive
            };
        }

        public async Task<CustomerDetailsDto?> GetCustomerDetailsAsync(int customerId, CancellationToken cancellationToken = default)
        {
            var info = await GetCustomerByIdAsync(customerId, cancellationToken);
            if (info == null) return null;

            int? companyId = await ResolveSoftCompanyIdAsync(cancellationToken);
            var summary = await _customerRepository.GetFinancialSummaryAsync(customerId, companyId, cancellationToken);

            return new CustomerDetailsDto
            {
                Info = info,
                FinancialSummary = summary ?? new CustomerFinancialSummaryDto
                {
                    CustomerID = info.CustomerID,
                    ShopName = info.ShopName,
                    CreditLimit = info.CreditLimit
                }
                // Note: History tabs (SalesHistory, PaymentHistory, CustomerLedger) are intentionally NULL here
                // to support fast page load. They are populated asynchronously via AJAX endpoints when tab is opened.
            };
        }

        public async Task<OperationResult<int>> CreateCustomerAsync(CreateCustomerDto dto, int userId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(dto.ShopName))
            {
                return OperationResult<int>.Fail("Shop Name is required.");
            }

            if (await _customerRepository.ExistsByShopNameAsync(dto.ShopName, null, cancellationToken))
            {
                return OperationResult<int>.Fail($"A customer with the shop name '{dto.ShopName}' already exists.");
            }

            if (!string.IsNullOrWhiteSpace(dto.Phone) && await _customerRepository.ExistsByPhoneAsync(dto.Phone, null, cancellationToken))
            {
                return OperationResult<int>.Fail($"A customer with the phone number '{dto.Phone}' already exists.");
            }

            var preferredResult = NormalizePreferredDiscount(dto.PreferredDiscountPercent);
            if (!preferredResult.Success)
            {
                return OperationResult<int>.Fail(preferredResult.Message!);
            }

            var customer = new Customer
            {
                ShopName = dto.ShopName.Trim(),
                OwnerName = dto.OwnerName?.Trim(),
                Phone = dto.Phone?.Trim(),
                Address = dto.Address?.Trim(),
                AreaID = dto.AreaID,
                SubAreaID = dto.SubAreaID,
                TaxID = dto.TaxID?.Trim(),
                CreditLimit = dto.CreditLimit < 0 ? 0m : dto.CreditLimit,
                PreferredDiscountPercent = preferredResult.Data,
                IsActive = dto.IsActive,
                CreatedBy = userId,
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false
            };

            await _customerRepository.AddAsync(customer, cancellationToken);
            await _customerRepository.SaveChangesAsync(cancellationToken);

            return OperationResult<int>.Ok(customer.CustomerID, "Customer created successfully.");
        }

        public async Task<OperationResult> UpdateCustomerAsync(EditCustomerDto dto, int userId, CancellationToken cancellationToken = default)
        {
            var customer = await _customerRepository.GetByIdAsync(dto.CustomerID, cancellationToken);
            if (customer == null)
            {
                return OperationResult.Fail("Customer not found.");
            }

            if (string.IsNullOrWhiteSpace(dto.ShopName))
            {
                return OperationResult.Fail("Shop Name is required.");
            }

            if (await _customerRepository.ExistsByShopNameAsync(dto.ShopName, dto.CustomerID, cancellationToken))
            {
                return OperationResult.Fail($"A customer with the shop name '{dto.ShopName}' already exists.");
            }

            if (!string.IsNullOrWhiteSpace(dto.Phone) && await _customerRepository.ExistsByPhoneAsync(dto.Phone, dto.CustomerID, cancellationToken))
            {
                return OperationResult.Fail($"A customer with the phone number '{dto.Phone}' already exists.");
            }

            var preferredResult = NormalizePreferredDiscount(dto.PreferredDiscountPercent);
            if (!preferredResult.Success)
            {
                return OperationResult.Fail(preferredResult.Message!);
            }

            customer.ShopName = dto.ShopName.Trim();
            customer.OwnerName = dto.OwnerName?.Trim();
            customer.Phone = dto.Phone?.Trim();
            customer.Address = dto.Address?.Trim();
            customer.AreaID = dto.AreaID;
            customer.SubAreaID = dto.SubAreaID;
            customer.TaxID = dto.TaxID?.Trim();
            customer.CreditLimit = dto.CreditLimit < 0 ? 0m : dto.CreditLimit;
            customer.PreferredDiscountPercent = preferredResult.Data;
            customer.IsActive = dto.IsActive;
            customer.UpdatedBy = userId;

            await _customerRepository.UpdateAsync(customer, cancellationToken);
            await _customerRepository.SaveChangesAsync(cancellationToken);

            return OperationResult.Ok("Customer updated successfully.");
        }

        public async Task<OperationResult> SoftDeleteCustomerAsync(int customerId, int userId, CancellationToken cancellationToken = default)
        {
            var customer = await _customerRepository.GetByIdAsync(customerId, cancellationToken);
            if (customer == null)
            {
                return OperationResult.Fail("Customer not found.");
            }

            bool hasHistory = await _customerRepository.HasHistoricalTransactionsAsync(customerId, cancellationToken);
            if (hasHistory)
            {
                return OperationResult.Fail("Cannot delete customer because historical invoices, payments, or ledger transactions exist. Please set the customer status to inactive instead.");
            }

            await _customerRepository.SoftDeleteAsync(customerId, userId, cancellationToken);
            await _customerRepository.SaveChangesAsync(cancellationToken);

            return OperationResult.Ok("Customer deleted successfully.");
        }

        public async Task<bool> ValidateShopNameAsync(string shopName, int? excludeCustomerId = null, CancellationToken cancellationToken = default)
        {
            return !await _customerRepository.ExistsByShopNameAsync(shopName, excludeCustomerId, cancellationToken);
        }

        public async Task<bool> ValidatePhoneAsync(string phone, int? excludeCustomerId = null, CancellationToken cancellationToken = default)
        {
            return !await _customerRepository.ExistsByPhoneAsync(phone, excludeCustomerId, cancellationToken);
        }

        public async Task<PagedResult<CustomerSalesHistoryDto>> GetSalesHistoryAsync(int customerId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        {
            int? companyId = await ResolveSoftCompanyIdAsync(cancellationToken);
            return await _customerRepository.GetSalesHistoryAsync(customerId, pageNumber, pageSize, companyId, cancellationToken);
        }

        public async Task<PagedResult<CustomerPaymentHistoryDto>> GetPaymentHistoryAsync(int customerId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        {
            int? companyId = await ResolveSoftCompanyIdAsync(cancellationToken);
            return await _customerRepository.GetPaymentHistoryAsync(customerId, pageNumber, pageSize, companyId, cancellationToken);
        }

        public async Task<PagedResult<CustomerLedgerEntryDto>> GetLedgerAsync(int customerId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        {
            int? companyId = await ResolveSoftCompanyIdAsync(cancellationToken);
            return await _customerRepository.GetLedgerAsync(customerId, pageNumber, pageSize, companyId, cancellationToken);
        }

        public async Task<CustomerFinancialSummaryDto?> GetFinancialSummaryAsync(int customerId, CancellationToken cancellationToken = default)
        {
            int? companyId = await ResolveSoftCompanyIdAsync(cancellationToken);
            return await _customerRepository.GetFinancialSummaryAsync(customerId, companyId, cancellationToken);
        }

        /// <summary>
        /// Null or &lt;= 0 → unset (null). Values must be in (0, 100].
        /// </summary>
        private static OperationResult<decimal?> NormalizePreferredDiscount(decimal? value)
        {
            if (!value.HasValue || value.Value <= 0m)
            {
                return OperationResult<decimal?>.Ok(null);
            }

            if (value.Value > 100m)
            {
                return OperationResult<decimal?>.Fail("Preferred discount percentage cannot exceed 100.");
            }

            return OperationResult<decimal?>.Ok(Math.Round(value.Value, 2, MidpointRounding.AwayFromZero));
        }
    }
}
