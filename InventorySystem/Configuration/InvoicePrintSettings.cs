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
        public List<string> TermsUrdu { get; set; } = new List<string>
        {
            "مال کی واپسی صرف اصل رسید کے ساتھ قبول کی جائے گی۔",
            "استعمال شدہ یا کھلی ہوئی اشیاء واپس نہیں لی جائیں گی۔",
            "ادائیگی مقررہ تاریخ تک لازمی ہے۔",
            "چیک باؤنس کی صورت میں تمام اخراجات صارف کے ذمے ہوں گے۔",
            "ڈیلیوری کے وقت مال چیک کر لیں؛ بعد ازاں کمپنی ذمہ دار نہیں ہوگی۔",
            "قیمتوں میں کسی بھی وقت تبدیلی کا حق محفوظ ہے۔",
            "تنازع کی صورت میں کمپنی کا فیصلہ حتمی ہوگا۔"
        };
    }
}
