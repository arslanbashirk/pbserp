using PBS.ERP.Shared.Constants;

namespace PBS.ERP.Shared.Models
{
    // Generic row for dynamic data
    public class MyGeneric
    {
        public Dictionary<string, object> Values { get; set; } = new();
    }

    public class SurveyTableCreate
    {
        public string? Survey { get; set; }
        public string TableName { get; set; }
        public string? TableDescription { get; set; }
    }

    // Request model to create a table
    public class TableCreateRequest
    {
        public string? UID { get; set; }
        public string? Connection { get; set; }
        public string Table { get; set; }
        public string? Schema { get; set; } = "dbo";
        public string? Database { get; set; }   // Target DB
        public string? TableType { get; set; }
        public string? TableDescription { get; set; }
        public List<Field> Columns { get; set; }
    }

    public class BasicRequest
    {
        public EntityPermission Permission { get; set; }
        public Entity Entity { get; set; }
        public List<Field> Columns { get; set; } = new();
        public List<dynamic> Rows { get; set; }
    }

    public class AlterTableRequest:AddColumnRequest
    {
        public string Column { get; set; }
    }
    public class AddColumnRequest
    {
        public string Table { get; set; }
        public Field Meta { get; set; }
    }

    public class ImportColumnsRequest
    {
        public string Table { get; set; }

        public List<ImportColumns> Columns { get; set; }
    }

    public class FieldModified: FieldMetadata
    {
        public bool Add { get; set; }
        public bool Modify { get; set; }
        public bool Drop { get; set; }
    }

    public class UpdateColumnRequest
    {
        public List<Field> Column { get; set; } = new();
        public string Table { get; set; }
    }

    public class DropTableRequest
    {
        public string Table { get; set; }
    }
    public class DropColumnRequest: DropTableRequest
    {
        public string Table { get; set; }
        public string Column { get; set; }
    }

    public class UniqueRequest
    {
        public string Table { get; set; } = null!;

        public string UniqueGroup { get; set; } = null!;

        public List<string> Fields { get; set; } = new();
    }

    public class GenericSaveRequest
    {
        public string table { get; set; }
        public GenericRow row { get; set; }
        public string key { get; set; }
    }

    public class GenericDeleteRequest
    {
        public string table { get; set; }
        public string key { get; set; }
        public string id { get; set; }
    }

    public class AlterTableName
    {
        public string New { get; set; }
        public string Old { get; set; }
        public string? Description { get; set; }
    }

    // Generic row model for table data
    public class GenericRow
    {
        public Dictionary<string, object> Values { get; set; } = new();
    }

    // Model to represent a table with columns and rows
    public class GenericTableModel
    {
        public string Table { get; set; }
        public string TableDescription { get; set; }
        public List<string> Columns { get; set; } = new();
        public List<GenericRow> Rows { get; set; } = new();
        public string KeyColumn { get; set; }
    }

    // Model to store table statistics
    public class GenericTableStats: DatabaseTables
    {
        public string Type { get; set; }
        public string Description { get; set; }
        public int? Columns { get; set; }
        public int? Rows { get; set; }
    }

    public class DatabaseTables
    {
        public string UID { get; set; }
        public string IP { get; set; }
        public string Database { get; set; }
        public string Schema { get; set; }
        public string Name { get; set; }
    }
    public class FieldSortRequest
    {
        public string Table { get; set; } = string.Empty;

        public List<FieldSortModel> Fields { get; set; } = new();
    }

    public class FieldSortModel
    {
        public string UID { get; set; } = string.Empty;

        public string? ColumnName { get; set; }

        public int SectionNumber { get; set; }

        public string? SectionName { get; set; }

        public int SortOrder { get; set; }
        public int? Width { get; set; }
    }

}
