namespace PBS.ERP.Shared.Models
{
    public static class Constants
    {
        public const string Identity_Application_Scheme = "Identity.Application";
        public const string Pretext = "SYS";
        public const string TableType = Pretext + "_TABLE_TYPE2";
        public const string UserTable = Pretext+"_USER";
        public const string RoleTable = Pretext+"_ROLE";
        public const string UserRoleTable = Pretext+ "_USER_ROLE";
        public const string PermissionTable = Pretext+ "_PERMISSION";
        public const string EntityTable = "ENTITY";
        public const string FieldTable = "FIELD";
        public const string FileUploadFacility = "FileUpload";
        public static readonly DatabaseTables systemEntity = new DatabaseTables
        {
            UID = "e4f8c2a7-9b34-4d2e-a6c8-1f3d5e7b9a2f",
            IP = "None",
            Database = "None",
            Schema = "None",
            Name = "System"
        };
        public static readonly string[] systemFields = { "Connection","IP", "Database", "Schema" }; 
        public enum MappingEntities
        {
            DB_MAP_CONNECTION,
            DB_MAP_ENTITY,
            DB_MAP_FIELD
        }

        public enum SupportedDatabae
        {
            SQLSERVER
        }

        public static class ServerErrorMessages
        {
            public const string ErrorDB = "Database connection could not be established.";
        }
    }
}
