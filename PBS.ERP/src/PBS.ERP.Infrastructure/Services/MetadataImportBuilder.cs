using PBS.ERP.Shared.Models;
using System.Text.RegularExpressions;

namespace PBS.ERP.Infrastructure;

public static class MetadataImportBuilder
{
    private static readonly Dictionary<string, string> DisplayAliases =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["DOB"] = "Date Of Birth",
            ["CNIC"] = "CNIC",
            ["NIC"] = "CNIC",
            ["UID"] = "UID",
            ["Qty"] = "Quantity",
            ["Dept"] = "Department",
            ["Org"] = "Organization"
        };

    private static readonly string[] TextAreaKeywords =
    {
        "description","remarks","comment","comments","notes",
        "feedback","detail","details","reason","summary"
    };

    private static readonly string[] AmountKeywords =
    {
        "amount","price","cost","rate","production","salary",
        "fee","balance","total","subtotal","discount","tax"
    };

    private static readonly string[] QuantityKeywords =
    {
        "qty","quantity","count","stock"
    };

    private static readonly string[] EmailKeywords = { "email", "mail" };
    private static readonly string[] PhoneKeywords = { "phone", "mobile", "cell", "contact", "telephone" };

    private static readonly string[] AddressKeywords =
    {
        "address","street","city","country","province","district","location"
    };

    private static readonly string[] DateKeywords = { "date", "dob", "birthdate" };
    private static readonly string[] TimeKeywords = { "time", "timestamp" };

    private static readonly string[] SensitiveKeywords =
    {
        "password","pin","apikey","api_key","token","secret","hash"
    };

    private static readonly string[] ImageKeywords =
    {
        "image","photo","picture","logo","attachment","documentfile","file"
    };

    public static Field Build(
    string entityUid,
    ImportColumns item,
    string userName,
    int sortOrder)
    {
        var sql = ParseSqlType(item.SqlType);

        var analysis = AnalyzeColumn(
            item.ColumnName,
            sql.SqlType,
            sql.Length,
            sql.Scale);

        // =========================================================
        // OVERRIDES (InputType + DisplayLabel)
        // =========================================================

        var displayLabel =
            !string.IsNullOrWhiteSpace(item.DisplayLabel)
                ? item.DisplayLabel
                : analysis.DisplayLabel;

        var inputType =
            !string.IsNullOrWhiteSpace(item.InputType)
                ? item.InputType
                : analysis.InputType;

        // =========================================================
        // 🔥 REQUIRED FIX (NEW)
        // =========================================================
        var isRequired = ResolveIsRequired(item.IsRequired);
        var cleanColumnName = NormalizeColumnName(item.ColumnName);

        return new Field
        {
            UID = Guid.NewGuid().ToString(),

            Entity = entityUid,

            ColumnName = cleanColumnName,
            DisplayLabel = displayLabel,

            SqlType = sql.SqlType,

            MaxLength = analysis.MaxLength ?? sql.Length,
            DecimalPlaces = analysis.DecimalPlaces ?? sql.Scale,

            InputType = inputType,

            Placeholder = displayLabel,
            Tooltip = displayLabel,

            // 🔥 FIX APPLIED HERE
            IsRequired = isRequired,

            IsReadonly = false,
            IsComputed = false,

            IsForeignKey = analysis.IsForeignKey,

            DropdownSourceTable = analysis.DropdownSourceTable,
            DropdownValueColumn = analysis.DropdownValueColumn,
            DropdownTextColumn = analysis.DropdownTextColumn,

            MinLength = analysis.MinLength,
            MinValue = analysis.MinValue,
            MaxValue = analysis.MaxValue,
            RegexPattern = analysis.RegexPattern,

            DefaultValue = analysis.DefaultValue,
            DefaultExpression = analysis.DefaultExpression,

            AllowInsert = true,
            AllowUpdate = true,
            AllowDelete = true,

            ShowInList = analysis.ShowInList,
            ShowInMobile = analysis.ShowInMobile,

            IsSearchable = analysis.IsSearchable,
            IsSortable = analysis.IsSortable,

            Exportable = analysis.Exportable,
            Importable = analysis.Importable,

            SectionNumber = analysis.SectionNumber,
            SectionName = analysis.SectionName,

            SortOrder = sortOrder,

            CreatedBy = userName,
            CreatedTime = DateTime.Now,
            ModifiedBy = userName,
            ModifiedTime = DateTime.Now,

            IsDeleted = false
        };
    }

    private static bool ResolveIsRequired(object? value)
    {
        if (value == null)
            return false;

        return value switch
        {
            bool b => b,

            int i => i == 1,

            long l => l == 1,

            short s => s == 1,

            byte by => by == 1,

            string str => ParseString(str),

            _ => false
        };
    }

    private static bool ParseString(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        value = value.Trim();

        return value switch
        {
            "1" => true,
            "0" => false,

            _ => value.Equals("true", StringComparison.OrdinalIgnoreCase)
                 || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
                 || value.Equals("y", StringComparison.OrdinalIgnoreCase)
        };
    }

    private static ColumnAnalysis AnalyzeColumn(
        string columnName,
        string sqlType,
        int? length,
        int? scale)
    {
        var lower = columnName.ToLowerInvariant();

        var result = new ColumnAnalysis
        {
            DisplayLabel = BuildDisplayLabel(columnName),

            InputType = GetInputType(columnName, sqlType),

            SectionName = DetermineSection(lower),
            SectionNumber = DetermineSectionNumber(lower),

            ShowInList = true,
            ShowInMobile = true,
            IsSearchable = true,
            IsSortable = true,
            Exportable = true,
            Importable = true,

            MaxLength = length,
            DecimalPlaces = scale
        };

        if (LooksLikeForeignKey(columnName))
        {
            var table = ExtractReferenceTable(columnName);

            result.IsForeignKey = true;
            result.InputType = "select";

            //result.DropdownSourceTable = table;
            //result.DropdownValueColumn = "ID";
            //result.DropdownTextColumn = "Name";
        }

        if (ContainsAny(lower, SensitiveKeywords))
        {
            result.InputType = "password";
            result.ShowInList = false;
            result.IsSearchable = false;
            result.IsSortable = false;
            result.Exportable = false;
        }

        if (ContainsAny(lower, EmailKeywords))
        {
            result.RegexPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
            result.MaxLength ??= 250;
        }

        if (ContainsAny(lower, PhoneKeywords))
        {
            result.MaxLength ??= 20;
        }

        if (ContainsAny(lower, AmountKeywords) || ContainsAny(lower, QuantityKeywords))
        {
            result.MinValue = 0;
        }

        if (lower.Contains("rating"))
        {
            result.InputType = "range";
            result.MinValue = 1;
            result.MaxValue = 5;
        }

        if (lower == "gender")
            result.InputType = "radio";

        if (lower == "country")
            result.InputType = "select";

        if (lower == "isactive")
            result.DefaultValue = "1";

        if (lower == "isdeleted")
            result.DefaultValue = "0";

        if (lower == "createdtime")
            result.DefaultExpression = "GETDATE()";

        if (lower == "uid")
            result.DefaultExpression = "NEWID()";

        return result;
    }

    private static string GetInputType(string columnName, string sqlType)
    {
        var lower = columnName.ToLowerInvariant();

        if (lower.StartsWith("is"))
            return "boolean";

        if (ContainsAny(lower, EmailKeywords)) return "email";
        if (ContainsAny(lower, PhoneKeywords)) return "tel";
        if (ContainsAny(lower, DateKeywords)) return "date";
        if (ContainsAny(lower, TimeKeywords)) return "datetime-local";
        if (ContainsAny(lower, TextAreaKeywords)) return "textarea";
        if (ContainsAny(lower, ImageKeywords)) return "file";

        return sqlType switch
        {
            "bit" => "boolean",
            "tinyint" or "smallint" or "int" or "bigint" => "number",
            "decimal" or "float" or "money" => "number",
            "date" => "date",
            "datetime" or "datetime2" => "datetime-local",
            "varchar(max)" or "nvarchar(max)" or "text" or "ntext" => "textarea",
            _ => "text"
        };
    }

    private static bool LooksLikeForeignKey(string column)
        => Regex.IsMatch(column, @"(Id|ID|Uid|UID)$");

    private static string ExtractReferenceTable(string column)
        => Regex.Replace(column, @"(Id|ID|Uid|UID)$", "");

    private static string DetermineSection(string name)
    {
        if (ContainsAny(name, EmailKeywords) || ContainsAny(name, PhoneKeywords))
            return "Contact";

        if (ContainsAny(name, AddressKeywords))
            return "Address";

        if (ContainsAny(name, AmountKeywords))
            return "Financial";

        if (ContainsAny(name, TextAreaKeywords))
            return "Additional Information";

        return "General";
    }

    private static int DetermineSectionNumber(string name)
    {
        if (ContainsAny(name, EmailKeywords) || ContainsAny(name, PhoneKeywords))
            return 2;

        if (ContainsAny(name, AddressKeywords))
            return 3;

        if (ContainsAny(name, AmountKeywords))
            return 4;

        return 1;
    }

    private static bool ContainsAny(string value, IEnumerable<string> keywords)
        => keywords.Any(value.Contains);

    private static string BuildDisplayLabel(string column)
    {
        if (DisplayAliases.TryGetValue(column, out var alias))
            return alias;

        var label = Regex.Replace(column, @"([a-z])([A-Z])", "$1 $2");
        label = Regex.Replace(label, @"[_\-]", " ");

        return label.Trim();
    }

    private static SqlTypeInfo ParseSqlType(string sqlType)
    {
        sqlType = sqlType.Trim().ToLower();

        var result = new SqlTypeInfo { SqlType = sqlType };

        var decimalMatch = Regex.Match(sqlType, @"decimal\((\d+),(\d+)\)");
        if (decimalMatch.Success)
        {
            result.SqlType = "decimal";
            result.Length = int.Parse(decimalMatch.Groups[1].Value);
            result.Scale = int.Parse(decimalMatch.Groups[2].Value);
            return result;
        }

        var lengthMatch = Regex.Match(sqlType, @"([a-z]+)\((\d+)\)");
        if (lengthMatch.Success)
        {
            result.SqlType = lengthMatch.Groups[1].Value;
            result.Length = int.Parse(lengthMatch.Groups[2].Value);
        }

        return result;
    }
    private static string NormalizeColumnName(string column)
    {
        if (string.IsNullOrWhiteSpace(column))
            return string.Empty;

        column = column.Trim();

        // replace multiple spaces with single space
        column = Regex.Replace(column, @"\s+", " ");

        // remove spaces completely for DB column name
        column = column.Replace(" ", "");

        return column;
    }
    private sealed class ColumnAnalysis
    {
        public string DisplayLabel { get; set; } = "";
        public string InputType { get; set; } = "";
        public bool IsForeignKey { get; set; }
        public string? DropdownSourceTable { get; set; }
        public string? DropdownValueColumn { get; set; }
        public string? DropdownTextColumn { get; set; }
        public string SectionName { get; set; } = "General";
        public int SectionNumber { get; set; } = 1;
        public bool ShowInList { get; set; }
        public bool ShowInMobile { get; set; }
        public bool IsSearchable { get; set; }
        public bool IsSortable { get; set; }
        public bool Exportable { get; set; }
        public bool Importable { get; set; }
        public int? MaxLength { get; set; }
        public int? DecimalPlaces { get; set; }
        public int? MinLength { get; set; }
        public int? MinValue { get; set; }
        public int? MaxValue { get; set; }
        public string? RegexPattern { get; set; }
        public string? DefaultValue { get; set; }
        public string? DefaultExpression { get; set; }
    }

    private sealed class SqlTypeInfo
    {
        public string SqlType { get; set; } = "";
        public int? Length { get; set; }
        public int? Scale { get; set; }
    }
}