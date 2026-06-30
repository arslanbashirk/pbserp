using Microsoft.AspNetCore.Http;
using PBS.ERP.Infrastructure.Services;
using PBS.ERP.Shared.Models;

namespace PBS.ERP.Infrastructure.Interfaces
{
    public interface ISuperInterface
    {
        Task<ServiceResult> CreateTableAsync(TableCreateRequest request, string createdBy);
        Task<ServiceResult> RenameTableAsync(AlterTableName request, string user);
        Task<ServiceResult> ImportColumnsAsync(ImportColumnsRequest request, string createdBy);
        Task<ServiceResult> AddColumnAsync(AddColumnRequest request, string createdBy);
        Task<ServiceResult> AlterColumnAsync(AlterTableRequest request, string user);
        Task<ServiceResult> DropTableAsync(DropTableRequest request, string user);
        Task<ServiceResult> DropColumnAsync(DropColumnRequest request, string user);
        Task<ServiceResult> SetUniqueKeyAsync(UniqueRequest model, string user);
        Task<ServiceResult> LayoutFieldAsync(FieldSortRequest model, string user);
        Task<List<ServiceResult>> BulkUploadColumnsAsync(IFormFile file, string tableUid, string user);
    }
}
