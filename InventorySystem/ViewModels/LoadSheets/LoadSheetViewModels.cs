using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace InventorySystem.ViewModels.LoadSheets
{
    public class LoadSheetIndexViewModel
    {
        [Required(ErrorMessage = "Please select a bookie.")]
        [Range(1, int.MaxValue, ErrorMessage = "Please select a bookie.")]
        [Display(Name = "Bookie")]
        public int BrokerID { get; set; }

        [Required(ErrorMessage = "Please select a date.")]
        [DataType(DataType.Date)]
        [Display(Name = "Date")]
        public DateTime Date { get; set; } = DateTime.Today;

        [Display(Name = "Delivery Person")]
        public int? DeliveryPersonID { get; set; }

        public IEnumerable<SelectListItem> Brokers { get; set; } = new List<SelectListItem>();
        public IEnumerable<SelectListItem> DeliveryPersons { get; set; } = new List<SelectListItem>();
    }
}
