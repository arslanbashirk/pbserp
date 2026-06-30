using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PBS.ERP.Shared.Models
{
    public partial class Field
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ID { get; set; }

        [StringLength(100)]
        public string? UID { get; set; } = null!;

        [StringLength(100)]
        public string? Entity { get; set; } = null!;

        [Required]
        [StringLength(100)]
        public string ColumnName { get; set; } = null!;


        [StringLength(150)]
        public string? DisplayLabel { get; set; }

        [StringLength(100)]
        public string? Placeholder { get; set; }

        [StringLength(500)]
        public string? Tooltip { get; set; }

        public int? SectionNumber { get; set; } = 0;
        public int? Width { get; set; } = 0;

        [StringLength(100)]
        public string? SectionName { get; set; }

        public int? SortOrder { get; set; } = 0;

        [Required]
        [StringLength(100)]
        public string SqlType { get; set; } = null!;

        [StringLength(50)]
        public string? InputType { get; set; }

        public bool? IsRequired { get; set; } = false;

        public int? MinLength { get; set; }

        public int? MaxLength { get; set; }

        public int? MinValue { get; set; }

        
        public int? MaxValue { get; set; }

        public int? DecimalPlaces { get; set; }

        [StringLength(500)]
        public string? RegexPattern { get; set; }

        public bool? IsReadonly { get; set; } = false;

        public bool? IsComputed { get; set; } = false;

        public string? ComputedExpression { get; set; }

        [StringLength(255)]
        public string? DefaultValue { get; set; }

        [StringLength(255)]
        public string? DefaultExpression { get; set; }

        public bool? IsForeignKey { get; set; } = false;

        [StringLength(100)]
        public string? DropdownSourceTable { get; set; }

        [StringLength(100)]
        public string? DropdownValueColumn { get; set; }

        [StringLength(100)]
        public string? DropdownTextColumn { get; set; }

        [StringLength(500)]
        public string? DropdownWhere { get; set; }

        [StringLength(200)]
        public string? DropdownOrderBy { get; set; }

        public bool? IsMultiSelect { get; set; } = false;

        public bool? IncludeBlankOption { get; set; } = true;

        public string? CustomQuery { get; set; }

        public bool? AllowInsert { get; set; } = true;

        public bool? AllowUpdate { get; set; } = true;

        public bool? AllowDelete { get; set; } = true;

        public bool? ShowInList { get; set; } = true;

        public bool? IsSearchable { get; set; } = true;

        public bool? IsSortable { get; set; } = true;

        public bool? Exportable { get; set; } = true;

        public bool? Importable { get; set; } = true;

        public bool? ShowInMobile { get; set; } = true;

        [StringLength(100)]
        public string? CreatedBy { get; set; }

        public DateTime? CreatedTime { get; set; }

        [StringLength(100)]
        public string? ModifiedBy { get; set; }

        public DateTime? ModifiedTime { get; set; }

        [StringLength(100)]
        public string? DeletedBy { get; set; }

        [StringLength(100)]
        public string? UniqueGroup { get; set; }

        public DateTime? DeletedTime { get; set; }

        public bool? IsDeleted { get; set; } = false;

        [Timestamp] // ROWVERSION
        public byte[]? RowVersion { get; set; } = null!;
    }
}
