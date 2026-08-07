using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Rendering;
using InventorySystem.DTOs.Returns;

namespace InventorySystem.ViewModels.Returns
{
    public class ProcessManualSalesReturnViewModel
    {
        public ProcessManualSalesReturnRequest Request { get; set; } = new ProcessManualSalesReturnRequest();
        public IEnumerable<SelectListItem> Customers { get; set; } = new List<SelectListItem>();
        public IEnumerable<SelectListItem> Warehouses { get; set; } = new List<SelectListItem>();
        public IEnumerable<SelectListItem> Products { get; set; } = new List<SelectListItem>();
    }
}
