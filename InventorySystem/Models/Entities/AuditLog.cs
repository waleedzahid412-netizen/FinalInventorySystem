using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventorySystem.Models.Entities
{
    /// <summary>
    /// System audit trail tracking all user operations.
    /// Immutable — records are never updated or deleted (BR-041).
    /// OldValues/NewValues store JSON payloads of the affected record state.
    /// No IsDeleted on this table per SCHEMA.md.
    /// </summary>
    [Table("AuditLogs", Schema = "dbo")]
    public class AuditLog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int AuditID { get; set; }

        [ForeignKey("User")]
        public int UserID { get; set; }
        public virtual User User { get; set; } = null!;

        /// <summary>INSERT | UPDATE | DELETE</summary>
        [Required]
        [MaxLength(50)]
        public string Action { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string TableName { get; set; } = string.Empty;

        public int RecordID { get; set; }

        /// <summary>JSON snapshot of the record before the change.</summary>
        public string? OldValues { get; set; }

        /// <summary>JSON snapshot of the record after the change.</summary>
        public string? NewValues { get; set; }

        public DateTime ActionDate { get; set; } = DateTime.UtcNow;
    }
}
