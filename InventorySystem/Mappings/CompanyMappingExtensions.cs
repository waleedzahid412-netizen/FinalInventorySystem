using InventorySystem.DTOs.Companies;
using InventorySystem.ViewModels.Companies;

namespace InventorySystem.Mappings
{
    public static class CompanyMappingExtensions
    {
        public static CreateCompanyDto ToDto(this CreateCompanyViewModel model)
        {
            return new CreateCompanyDto
            {
                CompanyName = model.CompanyName,
                ContactPerson = model.ContactPerson,
                Phone = model.Phone,
                Email = model.Email,
                Address = model.Address,
                TaxID = model.TaxID,
                CreditLimit = model.CreditLimit
            };
        }

        public static EditCompanyDto ToDto(this EditCompanyViewModel model)
        {
            return new EditCompanyDto
            {
                CompanyID = model.CompanyID,
                CompanyName = model.CompanyName,
                ContactPerson = model.ContactPerson,
                Phone = model.Phone,
                Email = model.Email,
                Address = model.Address,
                TaxID = model.TaxID,
                CreditLimit = model.CreditLimit
            };
        }

        public static EditCompanyViewModel ToViewModel(this EditCompanyDto dto)
        {
            return new EditCompanyViewModel
            {
                CompanyID = dto.CompanyID,
                CompanyName = dto.CompanyName,
                ContactPerson = dto.ContactPerson,
                Phone = dto.Phone,
                Email = dto.Email,
                Address = dto.Address,
                TaxID = dto.TaxID,
                CreditLimit = dto.CreditLimit
            };
        }

        public static CompanyDetailsViewModel ToViewModel(this CompanyDetailsDto dto)
        {
            return new CompanyDetailsViewModel
            {
                Company = dto
            };
        }
    }
}
