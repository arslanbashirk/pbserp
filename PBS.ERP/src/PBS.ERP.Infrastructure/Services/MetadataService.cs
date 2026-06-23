using Dapper;
using PBS.ERP.Infrastructure.Interfaces;
using PBS.ERP.Shared.Models;
using System.Data;
using static PBS.ERP.Shared.Models.SurveyModel;

namespace PBS.ERP.Modules.Survey.Services
{
    public class MetadataService : IMetadata
    {
        private readonly IDbConnection _con;

        public MetadataService(IDbConnection con)
        {
            _con = con;
        }

        public async Task<List<Field>> GetFieldsAsync(string table)
        {
            var sql = $@"SELECT * FROM {Constants.FieldTable}
                         WHERE Entity = @table AND ISNULL(IsDeleted, 0) = 0
                         ORDER BY ISNULL(SectionNumber, 9), ISNULL(SortOrder, Id)";

            var data = await _con.QueryAsync<Field>(sql, new { table });
            return data.ToList();
        }

        public async Task<Entity> GetEntityAsync(string table)
        {
            var sql = $@"SELECT * FROM {Constants.EntityTable}
                         WHERE UID = @table AND ISNULL(IsDeleted, 0) = 0";

            return await _con.QueryFirstOrDefaultAsync<Entity>(sql, new { table });
        }

        public async Task<SurveyFormModel> GetFormTitlesAsync(string form)
        {
            var sql = @"
                SELECT s.UID as SurveyUID, s.ShortVersion, s.Name, q.Type as Questionnaire, 
                c.SortOrder as SectionNo, 
                c.Title, c.SubTitle, 
                f.UID,  f.SortOrder as FormNo, f.Heading, f.Type as FormType
                FROM [Survey] s 
                INNER JOIN [Questionnaire] q ON(s.UID=q.Survey AND s.IsDeleted = 0 AND q.IsDeleted = 0)
                INNER JOIN [Section] c ON(q.UID=c.Questionnaire AND c.IsDeleted = 0)
                INNER JOIN [Form] f ON(c.UID=f.Section AND f.IsDeleted = 0)
                WHERE f.IsActive = 1 AND f.UID=@form";

            var result = await _con.QueryAsync<SurveyFormModel>(sql, new { form });
            return result.FirstOrDefault();
        }

        public async Task<FormNextOrPreviousModel> GetNextandPrevious(string survey, string form)
        {
            var sql = @"
                ;WITH Forms AS
                (
                    SELECT
                        c.SortOrder AS SectionNo,
                        f.SortOrder AS FormNo,
                        f.UID AS FormUID,
                        ROW_NUMBER() OVER(ORDER BY c.SortOrder, f.SortOrder) AS RN
                    FROM Survey s
                    INNER JOIN Questionnaire q 
                        ON q.Survey = s.UID AND q.IsDeleted = 0
                    INNER JOIN Section c 
                        ON c.Questionnaire = q.UID AND c.IsDeleted = 0
                    INNER JOIN Form f 
                        ON f.Section = c.UID 
                        AND f.IsDeleted = 0
                        AND f.IsActive = 1
                    WHERE s.UID = @survey
                        AND s.IsDeleted = 0
                ),
                CurrentForm AS
                (
                    SELECT RN
                    FROM Forms
                    WHERE FormUID = @form
                )

                SELECT
                    n.SectionNo  AS NextSectionNo,
                    p.SectionNo  AS PreviousSectionNo,

                    n.FormNo     AS NextFormNo,
                    p.FormNo     AS PreviousFormNo,

                    n.FormUID    AS NextFormUID,
                    p.FormUID    AS PreviousFormUID

                FROM CurrentForm cf
                OUTER APPLY (
                    SELECT TOP 1 *
                    FROM Forms
                    WHERE RN > cf.RN
                    ORDER BY RN
                ) n
                OUTER APPLY (
                    SELECT TOP 1 *
                    FROM Forms
                    WHERE RN < cf.RN
                    ORDER BY RN DESC
                ) p";

            return await _con.QueryFirstOrDefaultAsync<FormNextOrPreviousModel>(
                sql,
                new { survey, form }
            );
        }


        public async Task<IEnumerable<IDictionary<string, object>>> GetFormDataAsync(string table, string? filter)
        {
            

            var sql = $"SELECT * FROM {table} WHERE ISNULL(IsDeleted, 0) = 0 ";
            if (filter != null)
            {
                filter = filter.Replace("{alias}.", "");
                sql += $" {filter} ";
            }

            var rows = await _con.QueryAsync(sql);

            return rows.Select(r => (IDictionary<string, object>)r);
        }

    }

    public class MetadataViewModel
    {
        public IEnumerable<IDictionary<string, object>>? Entry { get; set; }
        public AreaViewModel? Area { get; set; }
        public SurveyFormModel? Form { get; set; }
        public Entity? Entity { get; set; }
        public List<Field> Fields { get; set; }
        public FormNextOrPreviousModel? NextPrevious { get; set; }
    }
}
