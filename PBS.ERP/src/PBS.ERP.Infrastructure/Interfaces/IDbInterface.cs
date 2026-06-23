using PBS.ERP.Infrastructure.Services;

namespace PBS.ERP.Infrastructure.Interfaces
{
    public interface IDbInterface
    {
        Task<ServiceResult> CreateDatabaseAsync(string? databaseName, string user);

        Task<ServiceResult> RenameDatabaseAsync(
            string? oldDatabaseName,
            string? newDatabaseName,
            string user);

        Task<ServiceResult> DropDatabaseAsync(string? databaseName, string user);

        Task<string?> GetSurveyDatabaseNameAsync(string surveyUid);
        Task<string?> GetSurveyTableNameAsync(string uid);
    }
}