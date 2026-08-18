using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using InventorySystem.DTOs.Brokers;
using InventorySystem.DTOs.Common;
using InventorySystem.Models.Entities;
using InventorySystem.Repositories.Interfaces;
using InventorySystem.Services.Interfaces;

namespace InventorySystem.Services.Implementations
{
    public class BrokerService : IBrokerService
    {
        private readonly IBrokerRepository _brokerRepository;

        public BrokerService(IBrokerRepository brokerRepository)
        {
            _brokerRepository = brokerRepository;
        }

        public async Task<PagedResult<BrokerListItemDto>> GetPagedBrokersAsync(BrokerFilterDto filter, CancellationToken cancellationToken = default)
        {
            var paged = await _brokerRepository.GetPagedAsync(filter, cancellationToken);
            var items = paged.Items.Select(b => new BrokerListItemDto
            {
                BrokerID = b.BrokerID,
                Name = b.Name,
                Phone = b.Phone,
                IsActive = b.IsActive
            }).ToList();

            return new PagedResult<BrokerListItemDto>(items, paged.TotalCount, paged.PageNumber, paged.PageSize);
        }

        public async Task<EditBrokerDto?> GetBrokerForEditAsync(int brokerId, CancellationToken cancellationToken = default)
        {
            var broker = await _brokerRepository.GetByIdAsync(brokerId, cancellationToken);
            if (broker == null) return null;

            return new EditBrokerDto
            {
                BrokerID = broker.BrokerID,
                Name = broker.Name,
                Phone = broker.Phone,
                IsActive = broker.IsActive
            };
        }

        public async Task<OperationResult<int>> CreateBrokerAsync(CreateBrokerDto dto, CancellationToken cancellationToken = default)
        {
            var errors = await ValidateAsync(dto.Name, null, cancellationToken);
            if (errors.Any())
            {
                return OperationResult<int>.Fail(errors);
            }

            var broker = new Broker
            {
                Name = dto.Name.Trim(),
                Phone = string.IsNullOrWhiteSpace(dto.Phone) ? null : dto.Phone.Trim(),
                IsActive = dto.IsActive,
                IsDeleted = false
            };

            await _brokerRepository.AddAsync(broker, cancellationToken);
            await _brokerRepository.SaveChangesAsync(cancellationToken);

            return OperationResult<int>.Ok(broker.BrokerID, "Broker created successfully.");
        }

        public async Task<OperationResult> UpdateBrokerAsync(EditBrokerDto dto, CancellationToken cancellationToken = default)
        {
            var errors = await ValidateAsync(dto.Name, dto.BrokerID, cancellationToken);
            if (errors.Any())
            {
                return OperationResult.Fail(errors);
            }

            var broker = await _brokerRepository.GetByIdAsync(dto.BrokerID, cancellationToken);
            if (broker == null)
            {
                return OperationResult.Fail("Broker not found.");
            }

            broker.Name = dto.Name.Trim();
            broker.Phone = string.IsNullOrWhiteSpace(dto.Phone) ? null : dto.Phone.Trim();
            broker.IsActive = dto.IsActive;

            await _brokerRepository.UpdateAsync(broker, cancellationToken);
            await _brokerRepository.SaveChangesAsync(cancellationToken);

            return OperationResult.Ok("Broker updated successfully.");
        }

        public async Task<OperationResult> SoftDeleteBrokerAsync(int brokerId, CancellationToken cancellationToken = default)
        {
            var broker = await _brokerRepository.GetByIdAsync(brokerId, cancellationToken);
            if (broker == null)
            {
                return OperationResult.Fail("Broker not found.");
            }

            if (await _brokerRepository.HasSalesInvoicesAsync(brokerId, cancellationToken))
            {
                return OperationResult.Fail("Cannot delete broker with linked sales invoices.");
            }

            await _brokerRepository.SoftDeleteAsync(brokerId, cancellationToken);
            await _brokerRepository.SaveChangesAsync(cancellationToken);

            return OperationResult.Ok("Broker deleted successfully.");
        }

        private async Task<List<string>> ValidateAsync(string name, int? excludeBrokerId, CancellationToken cancellationToken)
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(name))
            {
                errors.Add("Broker name is required.");
            }
            else if (await _brokerRepository.ExistsByNameAsync(name, excludeBrokerId, cancellationToken))
            {
                errors.Add("A broker with this name already exists.");
            }
            return errors;
        }
    }
}
