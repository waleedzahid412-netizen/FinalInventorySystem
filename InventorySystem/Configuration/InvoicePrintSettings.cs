using System.Collections.Generic;

namespace InventorySystem.Configuration
{
    /// <summary>
    /// Distributor identity and static print copy for sales invoice PDFs.
    /// </summary>
    public class InvoicePrintSettings
    {
        public const string SectionName = "InvoicePrint";

        public string BusinessName { get; set; } = "Minsa Beauty Distributor";
        public string Phone { get; set; } = "0315-2323294";
        public string TermsTitleUrdu { get; set; } = "اہم ہدایات برائے صارفین";

        /// <summary>
        /// Keep empty here — list comes from appsettings.json only.
        /// Non-empty defaults get concatenated with config and duplicate every term on the PDF.
        /// </summary>
        public List<string> TermsUrdu { get; set; } = new List<string>();
    }
}
