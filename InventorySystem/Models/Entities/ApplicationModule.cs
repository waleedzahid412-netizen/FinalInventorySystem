using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventorySystem.Models.Entities
{
    [Table("ApplicationModules", Schema = "dbo")]
    public class ApplicationModule
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ApplicationModuleID { get; set; }

        [Required]
        [MaxLength(50)]
        public string ModuleKey { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string ModuleName { get; set; } = string.Empty;

        public int DisplayOrder { get; set; }

        public bool IsActive { get; set; } = true;

        public virtual ICollection<ApplicationPage> Pages { get; set; } = new List<ApplicationPage>();
    }
}
