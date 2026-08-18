using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using InventorySystem.DTOs.Brokers;
using InventorySystem.DTOs.Common;

namespace InventorySystem.ViewModels.Brokers
{
    public class BrokerListViewModel
    {
        public BrokerFilterDto Filter { get; set; } = new BrokerFilterDto();
        public PagedResult<BrokerListItemDto> Brokers { get; set; } = new PagedResult<BrokerListItemDto>(new List<BrokerListItemDto>(), 0, 1, 10);
    }

    public class CreateBrokerViewModel
    {
        [Required(ErrorMessage = "Broker name is required.")]
        [MaxLength(100)]
        [Display(Name = "Broker Name")]
        public string Name { get; set; } = string.Empty;

        [MaxLength(20)]
        [Display(Name = "Phone")]
        public string? Phone { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;
    }

    public class EditBrokerViewModel
    {
        public int BrokerID { get; set; }

        [Required(ErrorMessage = "Broker name is required.")]
        [MaxLength(100)]
        [Display(Name = "Broker Name")]
        public string Name { get; set; } = string.Empty;

        [MaxLength(20)]
        [Display(Name = "Phone")]
        public string? Phone { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;
    }
}
