using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventorySystem.Models.Entities
{
    /// <summary>
    /// Global person who takes orders from the wholesaler and delivers to customers.
    /// Formerly DeliveryPerson. Not company-scoped (unlike Booker). Not the purchase counterparty (Company).
    /// </summary>
    [Table("Suppliers", Schema = "dbo")]
    public class Supplier : IHasIsDeleted
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int SupplierID { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(15)]
        public string? CNIC { get; set; }

        [MaxLength(20)]
        public string? Phone { get; set; }

        [MaxLength(255)]
        public string? Address { get; set; }

        /// <summary>Employee | External</summary>
        [Required]
        [MaxLength(30)]
        public string Type { get; set; } = "Employee";

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(CreatedByUser))]
        public int? CreatedBy { get; set; }
        public virtual User? CreatedByUser { get; set; }

        public DateTime? UpdatedAt { get; set; }

        [ForeignKey(nameof(UpdatedByUser))]
        public int? UpdatedBy { get; set; }
        public virtual User? UpdatedByUser { get; set; }

        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }

        public virtual ICollection<SalesInvoice> SalesInvoices { get; set; } = new List<SalesInvoice>();
    }
}
