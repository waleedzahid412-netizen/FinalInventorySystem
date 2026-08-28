using System.Collections.Generic;

namespace InventorySystem.ViewModels.CompanyScope
{
    public class CompanyScopeSelectViewModel
    {
        public string ReturnUrl { get; set; } = "/";
        public List<CompanyScopeOptionViewModel> Companies { get; set; } = new();
    }

    public class CompanyScopeOptionViewModel
    {
        public int CompanyID { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public bool IsCurrent { get; set; }
    }
}
