namespace PBS.ERP.Shared.Models
{
    public class ImportColumns
    {
        public string ColumnName { get; set; }
        public string SqlType { get; set; }
        public string? InputType { get; set; }
        public string? DisplayLabel { get; set; }
        public bool? IsRequired { get; set; }
    }
}
