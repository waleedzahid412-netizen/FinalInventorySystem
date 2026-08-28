using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using InventorySystem.DTOs.Bookers;
using InventorySystem.DTOs.Common;
using InventorySystem.Models.Entities;
using InventorySystem.Repositories.Interfaces;
using InventorySystem.Services.Interfaces;

namespace InventorySystem.Services.Implementations
{
    public class BookerService : IBookerService
    {
        private static readonly Regex CnicRegex = new(@"^\d{5}-\d{7}-\d$", RegexOptions.Compiled);

        private readonly IBookerRepository _bookerRepository;
        private readonly ICompanyContext _companyContext;

        public BookerService(IBookerRepository bookerRepository, ICompanyContext companyContext)
        {
            _bookerRepository = bookerRepository;
            _companyContext = companyContext;
        }

        public async Task<PagedResult<BookerListItemDto>> GetPagedBookersAsync(BookerFilterDto filter, CancellationToken cancellationToken = default)
        {
            if (!await _companyContext.TryResolveAsync(cancellationToken))
            {
                return new PagedResult<BookerListItemDto>(new List<BookerListItemDto>(), 0, filter.PageNumber, filter.PageSize);
            }

            // Specific company → filter; All Companies → leave 0 so repository shows all.
            filter.CompanyID = _companyContext.HasCompany ? _companyContext.CompanyID : 0;

            var paged = await _bookerRepository.GetPagedAsync(filter, cancellationToken);
            var items = new List<BookerListItemDto>();
            foreach (var b in paged.Items)
            {
                items.Add(new BookerListItemDto
                {
                    BookerID = b.BookerID,
                    Name = b.Name,
                    CNIC = b.CNIC,
                    Phone = b.Phone,
                    CreditLimit = b.CreditLimit,
                    OutstandingBalance = await _bookerRepository.GetOutstandingBalanceAsync(b.BookerID, cancellationToken),
                    IsActive = b.IsActive
                });
            }

            return new PagedResult<BookerListItemDto>(items, paged.TotalCount, paged.PageNumber, paged.PageSize);
        }

        public async Task<EditBookerDto?> GetBookerForEditAsync(int bookerId, CancellationToken cancellationToken = default)
        {
            var booker = await _bookerRepository.GetByIdAsync(bookerId, cancellationToken);
            if (booker == null) return null;

            bool hasInvoices = await _bookerRepository.HasSalesInvoicesAsync(bookerId, cancellationToken);

            return new EditBookerDto
            {
                BookerID = booker.BookerID,
                CompanyID = booker.CompanyID,
                Name = booker.Name,
                CNIC = booker.CNIC ?? string.Empty,
                Phone = booker.Phone,
                Address = booker.Address,
                CreditLimit = booker.CreditLimit,
                IsActive = booker.IsActive,
                CompanyName = booker.Company?.CompanyName ?? string.Empty,
                CanChangeCompany = !hasInvoices,
                OutstandingBalance = await _bookerRepository.GetOutstandingBalanceAsync(bookerId, cancellationToken)
            };
        }

        public async Task<OperationResult<int>> CreateBookerAsync(CreateBookerDto dto, int userId, CancellationToken cancellationToken = default)
        {
            if (!await _companyContext.TryResolveAsync(cancellationToken) || !_companyContext.HasCompany)
            {
                return OperationResult<int>.Fail("Select a specific company before creating.");
            }

            int companyId = _companyContext.CompanyID;
            var errors = await ValidateAsync(dto.Name, dto.CNIC, dto.CreditLimit, companyId, null, cancellationToken);
            if (errors.Any())
            {
                return OperationResult<int>.Fail(errors);
            }

            var booker = new Booker
            {
                CompanyID = companyId,
                Name = dto.Name.Trim(),
                CNIC = dto.CNIC.Trim(),
                Phone = string.IsNullOrWhiteSpace(dto.Phone) ? null : dto.Phone.Trim(),
                Address = string.IsNullOrWhiteSpace(dto.Address) ? null : dto.Address.Trim(),
                CreditLimit = dto.CreditLimit,
                IsActive = dto.IsActive,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userId,
                IsDeleted = false
            };

            await _bookerRepository.AddAsync(booker, cancellationToken);
            await _bookerRepository.SaveChangesAsync(cancellationToken);

            return OperationResult<int>.Ok(booker.BookerID, "Booker created successfully.");
        }

        public async Task<OperationResult> UpdateBookerAsync(EditBookerDto dto, int userId, CancellationToken cancellationToken = default)
        {
            var booker = await _bookerRepository.GetByIdAsync(dto.BookerID, cancellationToken);
            if (booker == null)
            {
                return OperationResult.Fail("Booker not found.");
            }

            var errors = await ValidateAsync(dto.Name, dto.CNIC, dto.CreditLimit, booker.CompanyID, dto.BookerID, cancellationToken);
            if (errors.Any())
            {
                return OperationResult.Fail(errors);
            }

            // CompanyID is never assigned here (BR-043 / never from form).
            booker.Name = dto.Name.Trim();
            booker.CNIC = dto.CNIC.Trim();
            booker.Phone = string.IsNullOrWhiteSpace(dto.Phone) ? null : dto.Phone.Trim();
            booker.Address = string.IsNullOrWhiteSpace(dto.Address) ? null : dto.Address.Trim();
            booker.CreditLimit = dto.CreditLimit;
            booker.IsActive = dto.IsActive;
            booker.UpdatedAt = DateTime.UtcNow;
            booker.UpdatedBy = userId;

            await _bookerRepository.UpdateAsync(booker, cancellationToken);
            await _bookerRepository.SaveChangesAsync(cancellationToken);

            return OperationResult.Ok("Booker updated successfully.");
        }

        public async Task<OperationResult> SoftDeleteBookerAsync(int bookerId, int userId, CancellationToken cancellationToken = default)
        {
            var booker = await _bookerRepository.GetByIdAsync(bookerId, cancellationToken);
            if (booker == null)
            {
                return OperationResult.Fail("Booker not found.");
            }

            if (await _bookerRepository.HasSalesInvoicesAsync(bookerId, cancellationToken))
            {
                return OperationResult.Fail("Cannot delete booker with linked sales invoices.");
            }

            await _bookerRepository.SoftDeleteAsync(bookerId, userId, cancellationToken);
            await _bookerRepository.SaveChangesAsync(cancellationToken);

            return OperationResult.Ok("Booker deleted successfully.");
        }

        private async Task<List<string>> ValidateAsync(
            string name,
            string? cnic,
            decimal creditLimit,
            int companyId,
            int? excludeBookerId,
            CancellationToken cancellationToken)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(name))
            {
                errors.Add("Booker name is required.");
            }
            else if (await _bookerRepository.ExistsByNameAsync(name, companyId, excludeBookerId, cancellationToken))
            {
                errors.Add("A booker with this name already exists for this company.");
            }

            if (string.IsNullOrWhiteSpace(cnic))
            {
                errors.Add("CNIC is required.");
            }
            else
            {
                var trimmed = cnic.Trim();
                if (!CnicRegex.IsMatch(trimmed))
                {
                    errors.Add("CNIC must be in format 12345-1234567-1.");
                }
                else if (await _bookerRepository.ExistsByCnicAsync(trimmed, excludeBookerId, cancellationToken))
                {
                    errors.Add("A booker with this CNIC already exists.");
                }
            }

            if (creditLimit < 0)
            {
                errors.Add("Credit limit cannot be negative.");
            }

            return errors;
        }
    }
}
