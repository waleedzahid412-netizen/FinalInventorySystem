using InventorySystem.DTOs.Customers;
using InventorySystem.ViewModels.Customers;

namespace InventorySystem.Mappings
{
    public static class CustomerMappingExtensions
    {
        public static CreateCustomerDto ToDto(this CreateCustomerViewModel model)
        {
            return new CreateCustomerDto
            {
                ShopName = model.ShopName,
                OwnerName = model.OwnerName,
                Phone = model.Phone,
                Address = model.Address,
                AreaID = model.AreaID,
                SubAreaID = model.SubAreaID,
                TaxID = model.TaxID,
                CreditLimit = model.CreditLimit,
                PreferredDiscountPercent = NormalizePreferredDiscount(model.PreferredDiscountPercent),
                IsActive = model.IsActive
            };
        }

        public static EditCustomerDto ToDto(this EditCustomerViewModel model)
        {
            return new EditCustomerDto
            {
                CustomerID = model.CustomerID,
                ShopName = model.ShopName,
                OwnerName = model.OwnerName,
                Phone = model.Phone,
                Address = model.Address,
                AreaID = model.AreaID,
                SubAreaID = model.SubAreaID,
                TaxID = model.TaxID,
                CreditLimit = model.CreditLimit,
                PreferredDiscountPercent = NormalizePreferredDiscount(model.PreferredDiscountPercent),
                IsActive = model.IsActive
            };
        }

        public static EditCustomerViewModel ToViewModel(this EditCustomerDto dto)
        {
            return new EditCustomerViewModel
            {
                CustomerID = dto.CustomerID,
                ShopName = dto.ShopName,
                OwnerName = dto.OwnerName,
                Phone = dto.Phone,
                Address = dto.Address,
                AreaID = dto.AreaID,
                SubAreaID = dto.SubAreaID,
                TaxID = dto.TaxID,
                CreditLimit = dto.CreditLimit,
                PreferredDiscountPercent = dto.PreferredDiscountPercent,
                IsActive = dto.IsActive
            };
        }

        /// <summary>Empty/0 preferred discount is stored as null (unset).</summary>
        private static decimal? NormalizePreferredDiscount(decimal? value)
        {
            if (!value.HasValue || value.Value <= 0m)
            {
                return null;
            }

            return value.Value;
        }

        public static CustomerDetailsViewModel ToViewModel(this CustomerDetailsDto dto)
        {
            return new CustomerDetailsViewModel
            {
                Details = dto
            };
        }
    }
}
