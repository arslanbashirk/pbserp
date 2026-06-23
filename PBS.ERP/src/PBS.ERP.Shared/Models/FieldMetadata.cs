
namespace PBS.ERP.Shared.Constants
{
    public partial class FieldMetadata
    {
        public long Id { get; set; }
        public string TableName { get; set; } = string.Empty;
        public string ColumnName { get; set; } = string.Empty;
        public string? DisplayLabel { get; set; }
        public string? Placeholder { get; set; }
        public string? Tooltip { get; set; }
        public int? SectionNumber { get; set; }
        public string? SectionName { get; set; }
        public int? SortOrder { get; set; }
        public string? SqlType { get; set; }
        public string? InputType { get; set; }
        public bool? IsRequired { get; set; }
        public int? MinLength { get; set; }
        public int? MaxLength { get; set; }
        public decimal? MinValue { get; set; }
        public decimal? MaxValue { get; set; }
        public int? DecimalPlaces { get; set; }
        public string? RegexPattern { get; set; }
        public bool? IsReadonly { get; set; }
        public bool? IsComputed { get; set; }
        public string? ComputedExpression { get; set; }
        public string? DefaultValue { get; set; }
        public string? DefaultExpression { get; set; }
        public bool? IsForeignKey { get; set; }
        public string? DropdownSourceTable { get; set; }
        public string? DropdownValueColumn { get; set; }
        public string? DropdownTextColumn { get; set; }
        public string? DropdownWhere { get; set; }
        public string? DropdownOrderBy { get; set; }
        public string? DependentOnColumn { get; set; }
        public string? DependentOnTable { get; set; }
        public bool? IsMultiSelect { get; set; }
        public bool? IncludeBlankOption { get; set; }
        public string? CustomQuery { get; set; }
        public bool? AllowInsert { get; set; }
        public bool? AllowUpdate { get; set; }
        public bool? ShowInList { get; set; }
        public bool? IsSearchable { get; set; }
        public bool? IsSortable { get; set; }
        public bool? Exportable { get; set; }
        public bool? Importable { get; set; }
        public bool? ShowInMobile { get; set; }
        public string? RolesVisible { get; set; }
        public string? RolesEditable { get; set; }
        public bool? AuditLog { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string? UpdatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool? IsDeleted { get; set; }
    }
}
