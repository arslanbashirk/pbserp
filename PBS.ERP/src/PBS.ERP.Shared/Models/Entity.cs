using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PBS.ERP.Shared.Models
{
    [Table("Entity")]
    public partial class Entity: AuditEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ID { get; set; }

        [StringLength(100)]
        public string? Connection { get; set; }

        [Required]
        [StringLength(100)]
        public string Server { get; set; } = null!;

        [Required]
        [StringLength(100)]
        public string Database { get; set; } = null!;

        [Required]
        [StringLength(100)]
        public string Schema { get; set; } = "dbo";

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = null!;

        [Required]
        [StringLength(100)]
        public string Description { get; set; } = null!;

        [Required]
        [StringLength(100)]
        public string Type { get; set; } = "SYS";

        public bool RetainTable { get; set; } = true;
        public bool RetainColumns { get; set; } = true;
        public bool RetainRows { get; set; } = true;

        public int? SchemaVersion { get; set; } = 1;

        public bool IsDeprecated { get; set; } = false;
        public bool IsPartitioned { get; set; } = false;


    }
}