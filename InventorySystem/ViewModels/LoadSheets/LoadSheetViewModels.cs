using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace InventorySystem.ViewModels.LoadSheets
{
    public class LoadSheetIndexViewModel
    {
        [Required(ErrorMessage = "Please select a booker.")]
        [Range(1, int.MaxValue, ErrorMessage = "Please select a booker.")]
        [Display(Name = "Booker")]
        public int BookerID { get; set; }

        [Required(ErrorMessage = "Please select a date.")]
        [DataType(DataType.Date)]
        [Display(Name = "Date")]
        public DateTime Date { get; set; } = DateTime.Today;

        [Display(Name = "Supplier")]
        public int? SupplierID { get; set; }

        public IEnumerable<SelectListItem> Bookers { get; set; } = new List<SelectListItem>();
        public IEnumerable<SelectListItem> Suppliers { get; set; } = new List<SelectListItem>();
    }
}
