using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using InventorySystem.DTOs.Common;
using InventorySystem.DTOs.Suppliers;
using InventorySystem.Models.Entities;
using InventorySystem.Repositories.Interfaces;
using InventorySystem.Services.Interfaces;

namespace InventorySystem.Services.Implementations
{
    public class SupplierService : ISupplierService
    {
        private static readonly Regex CnicRegex = new(@"^\d{5}-\d{7}-\d$", RegexOptions.Compiled);
        private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase) { "Employee", "External" };

        private readonly ISupplierRepository _supplierRepository;

        public SupplierService(ISupplierRepository supplierRepository)
        {
            _supplierRepository = supplierRepository;
        }

        public async Task<PagedResult<SupplierListItemDto>> GetPagedSuppliersAsync(SupplierFilterDto filter, CancellationToken cancellationToken = default)
        {
            var paged = await _supplierRepository.GetPagedAsync(filter, cancellationToken);
            var items = paged.Items.Select(s => new SupplierListItemDto
            {
                SupplierID = s.SupplierID,
                Name = s.Name,
                CNIC = s.CNIC,
                Phone = s.Phone,
                Type = s.Type,
                IsActive = s.IsActive
            }).ToList();

            return new PagedResult<SupplierListItemDto>(items, paged.TotalCount, paged.PageNumber, paged.PageSize);
        }

        public async Task<EditSupplierDto?> GetSupplierForEditAsync(int supplierId, CancellationToken cancellationToken = default)
        {
            var supplier = await _supplierRepository.GetByIdAsync(supplierId, cancellationToken);
            if (supplier == null) return null;

            return new EditSupplierDto
            {
                SupplierID = supplier.SupplierID,
                Name = supplier.Name,
                CNIC = supplier.CNIC ?? string.Empty,
                Phone = supplier.Phone,
                Address = supplier.Address,
                Type = supplier.Type,
                IsActive = supplier.IsActive
            };
        }

        public async Task<OperationResult<int>> CreateSupplierAsync(CreateSupplierDto dto, int userId, CancellationToken cancellationToken = default)
        {
            var errors = await ValidateAsync(dto.Name, dto.CNIC, dto.Type, null, cancellationToken);
            if (errors.Any())
            {
                return OperationResult<int>.Fail(errors);
            }

            var supplier = new Supplier
            {
                Name = dto.Name.Trim(),
                CNIC = dto.CNIC.Trim(),
                Phone = string.IsNullOrWhiteSpace(dto.Phone) ? null : dto.Phone.Trim(),
                Address = string.IsNullOrWhiteSpace(dto.Address) ? null : dto.Address.Trim(),
                Type = NormalizeType(dto.Type),
                IsActive = dto.IsActive,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userId > 0 ? userId : null,
                IsDeleted = false
            };

            await _supplierRepository.AddAsync(supplier, cancellationToken);
            await _supplierRepository.SaveChangesAsync(cancellationToken);

            return OperationResult<int>.Ok(supplier.SupplierID, "Supplier created successfully.");
        }

        public async Task<OperationResult> UpdateSupplierAsync(EditSupplierDto dto, int userId, CancellationToken cancellationToken = default)
        {
            var supplier = await _supplierRepository.GetByIdAsync(dto.SupplierID, cancellationToken);
            if (supplier == null)
            {
                return OperationResult.Fail("Supplier not found.");
            }

            var errors = await ValidateAsync(dto.Name, dto.CNIC, dto.Type, dto.SupplierID, cancellationToken);
            if (errors.Any())
            {
                return OperationResult.Fail(errors);
            }

            supplier.Name = dto.Name.Trim();
            supplier.CNIC = dto.CNIC.Trim();
            supplier.Phone = string.IsNullOrWhiteSpace(dto.Phone) ? null : dto.Phone.Trim();
            supplier.Address = string.IsNullOrWhiteSpace(dto.Address) ? null : dto.Address.Trim();
            supplier.Type = NormalizeType(dto.Type);
            supplier.IsActive = dto.IsActive;
            supplier.UpdatedAt = DateTime.UtcNow;
            supplier.UpdatedBy = userId > 0 ? userId : null;

            await _supplierRepository.UpdateAsync(supplier, cancellationToken);
            await _supplierRepository.SaveChangesAsync(cancellationToken);

            return OperationResult.Ok("Supplier updated successfully.");
        }

        public async Task<OperationResult> SoftDeleteSupplierAsync(int supplierId, int userId, CancellationToken cancellationToken = default)
        {
            var supplier = await _supplierRepository.GetByIdAsync(supplierId, cancellationToken);
            if (supplier == null)
            {
                return OperationResult.Fail("Supplier not found.");
            }

            if (await _supplierRepository.HasSalesInvoicesAsync(supplierId, cancellationToken))
            {
                return OperationResult.Fail("Cannot delete supplier with linked sales invoices.");
            }

            await _supplierRepository.SoftDeleteAsync(supplierId, userId > 0 ? userId : null, cancellationToken);
            await _supplierRepository.SaveChangesAsync(cancellationToken);

            return OperationResult.Ok("Supplier deleted successfully.");
        }

        private async Task<List<string>> ValidateAsync(
            string name,
            string? cnic,
            string type,
            int? excludeSupplierId,
            CancellationToken cancellationToken)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(name))
            {
                errors.Add("Supplier name is required.");
            }
            else if (await _supplierRepository.ExistsByNameAsync(name, excludeSupplierId, cancellationToken))
            {
                errors.Add("A supplier with this name already exists.");
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
                else if (await _supplierRepository.ExistsByCnicAsync(trimmed, excludeSupplierId, cancellationToken))
                {
                    errors.Add("A supplier with this CNIC already exists.");
                }
            }

            if (string.IsNullOrWhiteSpace(type) || !AllowedTypes.Contains(type.Trim()))
            {
                errors.Add("Type must be Employee or External.");
            }

            return errors;
        }

        private static string NormalizeType(string type)
        {
            var trimmed = type.Trim();
            if (trimmed.Equals("External", StringComparison.OrdinalIgnoreCase)) return "External";
            return "Employee";
        }
    }
}
