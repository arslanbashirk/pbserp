
using PBS.ERP.Shared.Models;
using static PBS.ERP.Shared.Models.SurveyModel;

namespace PBS.ERP.Infrastructure.Interfaces
{
    public interface IMetadata
    {
        Task<List<Field>> GetFieldsAsync(string table);
        Task<Entity> GetEntityAsync(string table);
        Task<SurveyFormModel> GetFormTitlesAsync(string form);
        Task<FormNextOrPreviousModel> GetNextandPrevious(string survey, string form);
        Task<IEnumerable<IDictionary<string, object>>> GetFormDataAsync(string form, string? filter);
    }

    
}
