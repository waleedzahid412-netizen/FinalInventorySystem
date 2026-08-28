using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventorySystem.Models.Entities
{
    [Table("ApplicationPages", Schema = "dbo")]
    public class ApplicationPage
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ApplicationPageID { get; set; }

        [ForeignKey(nameof(Module))]
        public int ApplicationModuleID { get; set; }
        public virtual ApplicationModule Module { get; set; } = null!;

        [Required]
        [MaxLength(80)]
        public string PageKey { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string PageName { get; set; } = string.Empty;

        [MaxLength(80)]
        public string? ControllerName { get; set; }

        [MaxLength(80)]
        public string? DefaultActionName { get; set; }

        public int DisplayOrder { get; set; }

        public bool IsActive { get; set; } = true;

        public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    }
}
