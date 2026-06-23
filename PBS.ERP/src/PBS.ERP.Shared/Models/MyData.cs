using System.Text.RegularExpressions;

namespace PBS.ERP.Shared.Models
{
    public class MyData
    {
        public static readonly HashSet<string> AllowedSqlTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "int", "int identity", "smallint", "bigint", "bit",
            "decimal", "float", "date", "datetime",
            "char", "varchar", "nchar", "nvarchar",
            "varchar(max)", "nvarchar(max)",
            "text", "ntext", "uniqueidentifier",
            "varbinary", "image", "xml"
        };

        public static string NormalizeSqlType(string sqlType)
        {
            return Regex.Replace(sqlType.Trim().ToLower(), @"\s+", " ");
        }
    }
}
