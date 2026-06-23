using Microsoft.AspNetCore.Mvc.ModelBinding;
using PBS.ERP.Shared.Enum;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PBS.ERP.Shared.Models
{
    [Table("EntityPermission")]
    public class EntityPermission: AuditEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long ID { get; set; }

        [Required]
        [StringLength(100)]
        public string EntityUID { get; set; }

        [BindNever]
        [ForeignKey(nameof(EntityUID))]
        public virtual Entity Entity { get; set; } = null!;

        [StringLength(100)]
        public string? RoleId { get; set; }

        [StringLength(100)]
        public string? UserId { get; set; }

        // ==============================
        // BASIC CRUD
        // ==============================

        public bool CanAccess { get; set; } = true;

        public bool CanSelect { get; set; } = true;
        public bool CanInsert { get; set; } = true;
        public bool CanUpdate { get; set; } = true;
        public bool CanDelete { get; set; } = true;

        public bool CanAlter { get; set; } = true;
        public bool CanDrop { get; set; } = true;
        public bool CanRename { get; set; } = true;
        public bool CanTruncate { get; set; } = false;


        public bool CanImport { get; set; } = true;
        public bool CanExport { get; set; } = true;

        public bool CanViewFields { get; set; } = true;
        public bool CanViewList { get; set; } = true;
        public bool CanViewDetail { get; set; } = true;
        public bool CanViewLog { get; set; } = false;
        public bool CanViewDeleted { get; set; } = false;

        public bool CanCustomizeColumns { get; set; } = false;
        public bool CanSaveViewLayout { get; set; } = false;

        // ==============================
        // COLUMN LEVEL CONTROL
        // ==============================

        [StringLength(2000)]
        public string? AllowedColumns { get; set; }
        // Example: "ID,Name,Status"

        [StringLength(2000)]
        public string? RestrictedColumns { get; set; }

        public bool MaskSensitiveData { get; set; } = false;

        // ==============================
        // FILTERING & QUERY CONTROL
        // ==============================

        public bool CanFilter { get; set; } = true;
        public bool CanUseAdvancedFilter { get; set; } = false;
        public bool CanUseGlobalSearch { get; set; } = true;

        public bool CanUseAggregates { get; set; } = false;
        public bool CanUseGroupBy { get; set; } = false;
        public bool CanReferOther { get; set; } = false;
        public bool CanOtherRefer { get; set; } = false;

        public bool CanSort { get; set; } = true;

        public bool CanQueryRawSql { get; set; } = false;
        public bool CanExecuteStoredProcedure { get; set; } = false;

        
        public DataScope DataScope { get; set; } = DataScope.All;

        [StringLength(2000)]
        public string? RowFilterExpression { get; set; }

        [StringLength(2000)]
        public string? SubQueryExpression { get; set; }

        public GeographyScope GeographyScope { get; set; }
        [StringLength(2000)]
        public string? GeographyScopeExpression { get; set; }

        public AccessChannel AllowedAccessChannel { get; set; } = AccessChannel.Any;

        public ExportFormat ExportPermissions { get; set; } = ExportFormat.None;

        public bool CanViewRawData { get; set; }
        public bool CanViewAggregatedOnly { get; set; }

        public bool CanGenerateReport { get; set; } = false;

        // ==============================
        // WORKFLOW & APPROVAL
        // ==============================

        public bool RequiresApproval { get; set; } = false;
        public bool CanApprove { get; set; } = false;
        public bool CanReject { get; set; } = false;

        public bool CanReopen { get; set; } = false;
        public bool CanLockRecord { get; set; } = false;
        public bool CanUnlockRecord { get; set; } = false;

        // ==============================
        // PERFORMANCE & CONTROL
        // ==============================

        public int? MaxRowsPerQuery { get; set; }
        public int? MaxExportRows { get; set; }

        public int? QueryTimeoutSeconds { get; set; }

        public bool CanAccessApi { get; set; } = false;
        public bool CanAccessMobile { get; set; } = false;

        // ==============================
        // SECURITY & AUDIT
        // ==============================

        public bool AuditSelect { get; set; } = false;
        public bool AuditInsert { get; set; } = false;
        public bool AuditUpdate { get; set; } = false;
        public bool AuditDelete { get; set; } = false;

        public bool LogQuery { get; set; } = false;

        public bool SyncOfflineData { get; set; }
        public bool RegisteredDeviceOnly { get; set; }

        public int Priority { get; set; } = 1;

       
    }

    [Flags]
    public enum ExportFormat
    {
        None = 0,
        Excel = 1,
        Pdf = 2,
        Csv = 4,
        Json = 8,
        Others = 16
    }

    public enum DataScope
    {
        All = 0,
        Own = 1,
        Team = 2,
        Geography = 3,
        CustomExpression = 4
    }

    public enum AccessChannel
    {
        Any,
        WebOnly,
        MobileOnly,
        ApiOnly
    }
}