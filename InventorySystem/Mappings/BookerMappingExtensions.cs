using InventorySystem.DTOs.Bookers;
using InventorySystem.ViewModels.Bookers;

namespace InventorySystem.Mappings
{
    public static class BookerMappingExtensions
    {
        public static CreateBookerDto ToDto(this CreateBookerViewModel model)
        {
            return new CreateBookerDto
            {
                Name = model.Name,
                CNIC = model.CNIC,
                Phone = model.Phone,
                Address = model.Address,
                CreditLimit = model.CreditLimit,
                IsActive = model.IsActive
            };
        }

        public static EditBookerDto ToDto(this EditBookerViewModel model)
        {
            return new EditBookerDto
            {
                BookerID = model.BookerID,
                Name = model.Name,
                CNIC = model.CNIC,
                Phone = model.Phone,
                Address = model.Address,
                CreditLimit = model.CreditLimit,
                IsActive = model.IsActive
            };
        }

        public static EditBookerViewModel ToViewModel(this EditBookerDto dto)
        {
            return new EditBookerViewModel
            {
                BookerID = dto.BookerID,
                Name = dto.Name,
                CNIC = dto.CNIC,
                Phone = dto.Phone,
                Address = dto.Address,
                CreditLimit = dto.CreditLimit,
                IsActive = dto.IsActive,
                CompanyName = dto.CompanyName,
                CanChangeCompany = dto.CanChangeCompany,
                OutstandingBalance = dto.OutstandingBalance
            };
        }
    }
}
