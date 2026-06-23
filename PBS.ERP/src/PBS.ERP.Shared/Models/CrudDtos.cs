using PBS.ERP.Shared.Models;

namespace PBS.ERP.Shared;

public sealed record ApiResponse(
    bool Success,
    string Message,
    object? Data = null,
    object? Errors=null
);

public sealed class CrudSaveRequest
{
    public string? ID { get; set; }
    public Dictionary<string, object?> Fields { get; set; } = new();
}

public sealed class CrudBulkSaveRequest
{
    public List<Dictionary<string, object?>> Rows { get; set; } = new();
}

public sealed class CleanRequest
{
    public string Id { get; set; } = "";
    public Dictionary<string, string> Fields { get; set; } = new();
}

public sealed class FileUploadGridDto
{
    public string UID { get; set; } = "";
    public string Title { get; set; } = "";
    public string TableReference { get; set; } = "";
    public string ByColumns { get; set; } = "";
    public string ForColumns { get; set; } = "";
    public string? ColumnTypes { get; set; }
    public string? LockedColumns { get; set; }
    public string? FormatTable { get; set; }
    public string? FormatColumns { get; set; }
    public string? FormatFilter { get; set; }
}


public class FileUploadGrid
{
    public string UID { get; set; } = "";
    public string Title { get; set; } = "";
    public string TableReference { get; set; } = "";
    public string ByColumns { get; set; } = "";
    public string ForColumns { get; set; } = "";
    public string? ColumnTypes { get; set; }
    public string? LockedColumns { get; set; }
    public string? FormatTable { get; set; }
    public string? FormatColumns { get; set; }
    public string? FormatFilter { get; set; }
}

public class FileTableView
{
    public string Title { get; set; } = "";
    public string Columns { get; set; } = "";
    public Entity Entity { get; set; } = new();
    public FileUploadGrid Grid { get; set; } = new();
    public Dictionary<string, string> Reference { get; set; } = new();
    public Dictionary<string, string> fields { get; set; } = new();
    public string? FormatColumns { get; set; }
    public string? FormatTable { get; set; }
    public string? FormatFilter { get; set; }
}
