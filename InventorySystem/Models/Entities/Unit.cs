using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventorySystem.Models.Entities
{
    [Table("Units", Schema = "dbo")]
    public class Unit : IHasIsDeleted
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int UnitID { get; set; }

        [Required]
        [MaxLength(50)]
        public string UnitName { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        // ===== AUDIT FIELDS =====
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("CreatedByUser")]
        public int? CreatedBy { get; set; }
        public virtual User? CreatedByUser { get; set; }

        public DateTime? UpdatedAt { get; set; }

        [ForeignKey("UpdatedByUser")]
        public int? UpdatedBy { get; set; }
        public virtual User? UpdatedByUser { get; set; }

        // ===== SOFT DELETE =====
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }

        // ===== RELATIONSHIPS =====
        // Products where this unit is the BASE unit
        public virtual ICollection<Product> ProductsAsBaseUnit { get; set; } = new List<Product>();
        // ProductUnits conversion records using this unit
        public virtual ICollection<ProductUnit> ProductUnits { get; set; } = new List<ProductUnit>();
    }
}
