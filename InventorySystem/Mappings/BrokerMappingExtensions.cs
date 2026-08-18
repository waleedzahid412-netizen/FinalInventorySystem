using InventorySystem.DTOs.Brokers;
using InventorySystem.ViewModels.Brokers;

namespace InventorySystem.Mappings
{
    public static class BrokerMappingExtensions
    {
        public static CreateBrokerDto ToDto(this CreateBrokerViewModel model)
        {
            return new CreateBrokerDto
            {
                Name = model.Name,
                Phone = model.Phone,
                IsActive = model.IsActive
            };
        }

        public static EditBrokerDto ToDto(this EditBrokerViewModel model)
        {
            return new EditBrokerDto
            {
                BrokerID = model.BrokerID,
                Name = model.Name,
                Phone = model.Phone,
                IsActive = model.IsActive
            };
        }

        public static EditBrokerViewModel ToViewModel(this EditBrokerDto dto)
        {
            return new EditBrokerViewModel
            {
                BrokerID = dto.BrokerID,
                Name = dto.Name,
                Phone = dto.Phone,
                IsActive = dto.IsActive
            };
        }
    }
}
