using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventorySystem.Models.Entities
{
    /// <summary>
    /// Delivery agent assigned to sales invoices for order fulfillment.
    /// Delivery personnel perform no inventory operations (BR per business overview).
    /// </summary>
    [Table("DeliveryPersons", Schema = "dbo")]
    public class DeliveryPerson : IHasIsDeleted
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int DeliveryPersonID { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? Phone { get; set; }

        /// <summary>Employee | External</summary>
        [Required]
        [MaxLength(30)]
        public string Type { get; set; } = "Employee";

        public bool IsActive { get; set; } = true;

        // ===== SOFT DELETE =====
        // Note: No audit columns (CreatedAt/CreatedBy) in SCHEMA.md for this table
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }

        // ===== RELATIONSHIPS =====
        public virtual ICollection<SalesInvoice> SalesInvoices { get; set; } = new List<SalesInvoice>();
    }
}
