using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventorySystem.Models.Entities
{
    [Table("ChequeStatusAudits", Schema = "dbo")]
    public class ChequeStatusAudit
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ChequeStatusAuditID { get; set; }

        [Required]
        [MaxLength(20)]
        public string PaymentType { get; set; } = string.Empty; // "Customer" or "Company"

        public int PaymentID { get; set; } // CustomerPaymentID or CompanyPaymentID

        [Required]
        [MaxLength(20)]
        public string PreviousStatus { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string NewStatus { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Remarks { get; set; }

        [MaxLength(500)]
        public string? Reason { get; set; }

        [MaxLength(100)]
        public string? ReferenceNumber { get; set; }

        [ForeignKey("UpdatedByUser")]
        public int UpdatedBy { get; set; }
        public virtual User UpdatedByUser { get; set; } = null!;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
