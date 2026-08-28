using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventorySystem.Models.Entities
{
    [Table("RolePermissions", Schema = "dbo")]
    public class RolePermission
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int RolePermissionID { get; set; }

        [ForeignKey(nameof(Role))]
        public int RoleID { get; set; }
        public virtual Role Role { get; set; } = null!;

        [ForeignKey(nameof(Page))]
        public int ApplicationPageID { get; set; }
        public virtual ApplicationPage Page { get; set; } = null!;

        public bool CanView { get; set; }
        public bool CanAdd { get; set; }
        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }
    }
}
