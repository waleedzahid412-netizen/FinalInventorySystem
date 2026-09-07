using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using InventorySystem.DTOs.Common;
using InventorySystem.DTOs.Companies;
using InventorySystem.Helpers;
using InventorySystem.Models.Entities;
using InventorySystem.Repositories.Interfaces;
using InventorySystem.Services.Interfaces;

namespace InventorySystem.Services.Implementations
{
    public class CompanyService : ICompanyService
    {
        private readonly ICompanyRepository _companyRepository;
        private readonly ILogger<CompanyService> _logger;

        public CompanyService(
            ICompanyRepository companyRepository,
            ILogger<CompanyService> logger)
        {
            _companyRepository = companyRepository;
            _logger = logger;
        }

        public async Task<PagedResult<CompanyListItemDto>> GetPagedCompaniesAsync(CompanyFilterDto filter, CancellationToken cancellationToken = default)
        {
            var pagedCompanies = await _companyRepository.GetPagedAsync(filter, cancellationToken);

            var listItems = new List<CompanyListItemDto>();
            foreach (var company in pagedCompanies.Items)
            {
                var summary = await _companyRepository.GetFinancialSummaryAsync(company.CompanyID, cancellationToken);

                listItems.Add(new CompanyListItemDto
                {
                    CompanyID = company.CompanyID,
                    CompanyName = company.CompanyName,
                    ContactPerson = company.ContactPerson,
                    Phone = company.Phone,
                    Email = company.Email,
                    Address = company.Address,
                    OutstandingPayable = summary?.OutstandingPayable ?? 0m,
                    TotalPurchases = summary?.TotalPurchases ?? 0m,
                    CreatedAt = company.CreatedAt
                });
            }

            return new PagedResult<CompanyListItemDto>(listItems, pagedCompanies.TotalCount, pagedCompanies.PageNumber, pagedCompanies.PageSize);
        }

        public async Task<CompanyDetailsDto?> GetCompanyDetailsAsync(int companyId, CancellationToken cancellationToken = default)
        {
            var company = await _companyRepository.GetByIdAsync(companyId, cancellationToken);
            if (company == null) return null;

            var financialSummary = await _companyRepository.GetFinancialSummaryAsync(companyId, cancellationToken);

            return new CompanyDetailsDto
            {
                CompanyID = company.CompanyID,
                CompanyName = company.CompanyName,
                ContactPerson = company.ContactPerson,
                Phone = company.Phone,
                Email = company.Email,
                Address = company.Address,
                TaxID = company.TaxID,
                CreditLimit = company.CreditLimit,
                CreatedAt = company.CreatedAt,
                FinancialSummary = financialSummary
            };
        }

        public async Task<EditCompanyDto?> GetCompanyForEditAsync(int companyId, CancellationToken cancellationToken = default)
        {
            var company = await _companyRepository.GetByIdAsync(companyId, cancellationToken);
            if (company == null) return null;

            return new EditCompanyDto
            {
                CompanyID = company.CompanyID,
                CompanyName = company.CompanyName,
                ContactPerson = company.ContactPerson ?? string.Empty,
                Phone = company.Phone,
                Email = company.Email,
                Address = company.Address,
                TaxID = company.TaxID,
                CreditLimit = company.CreditLimit
            };
        }

        public async Task<OperationResult<int>> CreateCompanyAsync(CreateCompanyDto dto, int userId, CancellationToken cancellationToken = default)
        {
            var errors = await ValidateCreateDtoAsync(dto, cancellationToken);
            if (errors.Any())
            {
                return OperationResult<int>.Fail(errors);
            }

            try
            {
                var now = DateTime.UtcNow;
                var company = new Company
                {
                    CompanyName = dto.CompanyName.Trim(),
                    ContactPerson = dto.ContactPerson.Trim(),
                    Phone = string.IsNullOrWhiteSpace(dto.Phone) ? null : dto.Phone.Trim(),
                    Email = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim(),
                    Address = string.IsNullOrWhiteSpace(dto.Address) ? null : dto.Address.Trim(),
                    TaxID = string.IsNullOrWhiteSpace(dto.TaxID) ? null : dto.TaxID.Trim(),
                    CreditLimit = dto.CreditLimit,
                    CompanyPercentage = dto.CompanyPercentage,
                    CreatedAt = now,
                    IsDeleted = false
                };

                await _companyRepository.AddAsync(company, cancellationToken);
                await _companyRepository.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Company created successfully with ID {CompanyID}", company.CompanyID);
                return OperationResult<int>.Ok(company.CompanyID, "Company created successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while creating company {CompanyName}", dto.CompanyName);
                return OperationResult<int>.Fail(UserFacingErrorMessages.CompanyCreateFailed);
            }
        }

        public async Task<OperationResult> UpdateCompanyAsync(EditCompanyDto dto, int userId, CancellationToken cancellationToken = default)
        {
            var errors = await ValidateEditDtoAsync(dto, cancellationToken);
            if (errors.Any())
            {
                return OperationResult.Fail(errors);
            }

            var company = await _companyRepository.GetByIdAsync(dto.CompanyID, cancellationToken);
            if (company == null)
            {
                return OperationResult.Fail("Company not found.");
            }

            try
            {
                var now = DateTime.UtcNow;
                company.CompanyName = dto.CompanyName.Trim();
                company.ContactPerson = dto.ContactPerson.Trim();
                company.Phone = string.IsNullOrWhiteSpace(dto.Phone) ? null : dto.Phone.Trim();
                company.Email = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim();
                company.Address = string.IsNullOrWhiteSpace(dto.Address) ? null : dto.Address.Trim();
                company.TaxID = string.IsNullOrWhiteSpace(dto.TaxID) ? null : dto.TaxID.Trim();
                company.CreditLimit = dto.CreditLimit;
                company.UpdatedAt = now;
                company.UpdatedBy = userId;

                await _companyRepository.UpdateAsync(company, cancellationToken);
                await _companyRepository.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Company updated successfully for ID {CompanyID}", company.CompanyID);
                return OperationResult.Ok("Company updated successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while updating company ID {CompanyID}", dto.CompanyID);
                return OperationResult.Fail(UserFacingErrorMessages.CompanyUpdateFailed);
            }
        }

        public async Task<OperationResult> SoftDeleteCompanyAsync(int companyId, int userId, CancellationToken cancellationToken = default)
        {
            var company = await _companyRepository.GetByIdAsync(companyId, cancellationToken);
            if (company == null)
            {
                return OperationResult.Fail("Company not found.");
            }

            // SAFETY RULE: Block deletion if company has historical transactions
            bool hasTransactions = await _companyRepository.HasHistoricalTransactionsAsync(companyId, cancellationToken);
            if (hasTransactions)
            {
                return OperationResult.Fail("Cannot delete a company that has historical purchase invoices or payment history.");
            }

            try
            {
                await _companyRepository.SoftDeleteAsync(companyId, userId, cancellationToken);
                await _companyRepository.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Company soft-deleted successfully for ID {CompanyID}", companyId);
                return OperationResult.Ok("Company deleted successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while deleting company ID {CompanyID}", companyId);
                return OperationResult.Fail(UserFacingErrorMessages.CompanyDeleteFailed);
            }
        }

        public async Task<bool> ValidateCompanyNameAsync(string name, int? excludeCompanyId = null, CancellationToken cancellationToken = default)
        {
            return !await _companyRepository.ExistsByNameAsync(name, excludeCompanyId, cancellationToken);
        }

        public async Task<bool> ValidatePhoneAsync(string phone, int? excludeCompanyId = null, CancellationToken cancellationToken = default)
        {
            return !await _companyRepository.ExistsByPhoneAsync(phone, excludeCompanyId, cancellationToken);
        }

        public async Task<PagedResult<CompanyPurchaseHistoryDto>> GetPurchaseHistoryAsync(int companyId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        {
            return await _companyRepository.GetPurchaseHistoryAsync(companyId, pageNumber, pageSize, cancellationToken);
        }

        public async Task<PagedResult<CompanyPaymentHistoryDto>> GetPaymentHistoryAsync(int companyId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        {
            return await _companyRepository.GetPaymentHistoryAsync(companyId, pageNumber, pageSize, cancellationToken);
        }

        public async Task<PagedResult<CompanyLedgerEntryDto>> GetLedgerAsync(int companyId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        {
            return await _companyRepository.GetLedgerAsync(companyId, pageNumber, pageSize, cancellationToken);
        }

        private async Task<List<string>> ValidateCreateDtoAsync(CreateCompanyDto dto, CancellationToken cancellationToken)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(dto.CompanyName))
            {
                errors.Add("Company Name is required.");
            }

            if (string.IsNullOrWhiteSpace(dto.ContactPerson))
            {
                errors.Add("Contact Person is required.");
            }

            if (dto.CreditLimit < 0)
            {
                errors.Add("Credit Limit cannot be negative.");
            }

            if (!string.IsNullOrWhiteSpace(dto.CompanyName))
            {
                bool exists = await _companyRepository.ExistsByNameAsync(dto.CompanyName, null, cancellationToken);
                if (exists)
                {
                    errors.Add("Company Name already exists.");
                }
            }

            if (!string.IsNullOrWhiteSpace(dto.Phone))
            {
                bool phoneExists = await _companyRepository.ExistsByPhoneAsync(dto.Phone, null, cancellationToken);
                if (phoneExists)
                {
                    errors.Add("Phone number already exists.");
                }
            }

            return errors;
        }

        private async Task<List<string>> ValidateEditDtoAsync(EditCompanyDto dto, CancellationToken cancellationToken)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(dto.CompanyName))
            {
                errors.Add("Company Name is required.");
            }

            if (string.IsNullOrWhiteSpace(dto.ContactPerson))
            {
                errors.Add("Contact Person is required.");
            }

            if (dto.CreditLimit < 0)
            {
                errors.Add("Credit Limit cannot be negative.");
            }

            if (!string.IsNullOrWhiteSpace(dto.CompanyName))
            {
                bool exists = await _companyRepository.ExistsByNameAsync(dto.CompanyName, dto.CompanyID, cancellationToken);
                if (exists)
                {
                    errors.Add("Company Name already exists.");
                }
            }

            if (!string.IsNullOrWhiteSpace(dto.Phone))
            {
                bool phoneExists = await _companyRepository.ExistsByPhoneAsync(dto.Phone, dto.CompanyID, cancellationToken);
                if (phoneExists)
                {
                    errors.Add("Phone number already exists.");
                }
            }

            return errors;
        }
    }
}
