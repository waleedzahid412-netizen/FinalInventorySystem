using InventorySystem.DTOs.Suppliers;
using InventorySystem.ViewModels.Suppliers;

namespace InventorySystem.Mappings
{
    public static class SupplierMappingExtensions
    {
        public static CreateSupplierDto ToDto(this CreateSupplierViewModel model)
        {
            return new CreateSupplierDto
            {
                Name = model.Name,
                CNIC = model.CNIC,
                Phone = model.Phone,
                Address = model.Address,
                Type = model.Type,
                IsActive = model.IsActive
            };
        }

        public static EditSupplierDto ToDto(this EditSupplierViewModel model)
        {
            return new EditSupplierDto
            {
                SupplierID = model.SupplierID,
                Name = model.Name,
                CNIC = model.CNIC,
                Phone = model.Phone,
                Address = model.Address,
                Type = model.Type,
                IsActive = model.IsActive
            };
        }

        public static EditSupplierViewModel ToViewModel(this EditSupplierDto dto)
        {
            return new EditSupplierViewModel
            {
                SupplierID = dto.SupplierID,
                Name = dto.Name,
                CNIC = dto.CNIC,
                Phone = dto.Phone,
                Address = dto.Address,
                Type = dto.Type,
                IsActive = dto.IsActive
            };
        }
    }
}
