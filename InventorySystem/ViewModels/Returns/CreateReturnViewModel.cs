using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace InventorySystem.ViewModels.Returns
{
    public class CreateReturnViewModel
    {
        public IEnumerable<SelectListItem> Customers { get; set; } = new List<SelectListItem>();
        public IEnumerable<SelectListItem> Warehouses { get; set; } = new List<SelectListItem>();
        public IEnumerable<SelectListItem> Products { get; set; } = new List<SelectListItem>();
        public IEnumerable<SelectListItem> Invoices { get; set; } = new List<SelectListItem>();
    }
}
