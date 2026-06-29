using PBS.ERP.Shared.Identity;

namespace PBS.ERP.Shared.Models
{
    public class SurveyModel
    {
        public class MainModel { 
            public SurveyViewModel? Survey { get; set; }
            public List<QuestionnaireViewModel>? Questionnaires { get; set; }
            public List<SectionViewModel>? Sections { get; set; }
            public List<FormViewModel>? Forms { get; set; }
            public List<Mode>? Modes { get; set; }
        }

        public class PhaseFrameView
        {
            public string? PhaseUID { get; set; }
            public int? Phase { get; set; }
            public string? Title { get; set; }
            public string? UID { get; set; }
            public string? AreaCode { get; set; }
            public int? AreaLevel { get; set; }
            public string? AreaLevelName { get; set; }
            public int? Status { get; set; }
            public int? ReplacedAreaCode { get; set; }
        }

        public class Mode
        {
            public int Code { get; set; }
            public required string Title { get; set; }
            public bool IsSelected { get; set; }
        }

        public class FieldSurveyView
        {
            public string? PhaseUID { get; set; }
            public int? Phase { get; set; }
            public string? Title { get; set; }
            public string? UID { get; set; }
            public string? AreaCode { get; set; }
            public string? Status { get; set; }
            public int? ReplacedAreaCode { get; set; }
        }

        public class FieldView
        {
            public ApplicationUser? user { get; set; }
            public FieldSurvey? survey { get; set; }
            public FieldPhase? phase { get; set; }
            public List<FieldRole>? roles { get; set; }
            public List<FieldPPIISample>? sample { get; set; }
        }

        public class FieldSurvey
        {
            public string UID { get; set; } = "";
            public string Name { get; set; } = "";
            public string ShortVersion { get; set; } = "";
            public string ExecutionYear { get; set; } = "";
            public string ReferencePeriod { get; set; } = "";
        }

        public class FieldPhase
        {
            public string? UID { get; set; }
            public int? Phase { get; set; }
            public string? Title { get; set; }
            public string? Survey { get; set; }
            public string? Env { get; set; }
            public string? ReferencePeriod { get; set; }
            public DateTime? TestingClosingDate { get; set; }
            public DateTime? TrainingclosingDate { get; set; }
            public DateTime? FieldClosingDate { get; set; }
            public DateTime? FieldStartDate { get; set; }
            public int? IsActive { get; set; }
        }

        public class FieldRole
        {
            public string? UID { get; set; }
            public string? Name { get; set; }
            public string? Description { get; set; }
            public string? Priority { get; set; }
        }

        public class FieldPPIISample
        {
            // From Sample (f)
            public string PSIC4 { get; set; }
            public string PSIC { get; set; }
            public string Province { get; set; }
            public string District { get; set; }
            public string DistrictCode { get; set; }
            public string AreaCode { get; set; }
            public string Name { get; set; }
            public string Address { get; set; }
            public string Contact { get; set; }
            public int? Employee { get; set; }

            // From BasicInfo (b)
            public string? Form1 { get; set; }
            public string? Form2 { get; set; }
            public int? ReferBack { get; set; }
            public string? Respondent { get; set; }
            public DateTime? CreatedTime { get; set; }
            public DateTime? ModifiedTime { get; set; }
            public string CreatedBy { get; set; }
            public string ModifiedBy { get; set; }
        }

        public class EntityView
        {
            public ApplicationUser? user { get; set; }
            public FieldSurvey? survey { get; set; }
            public FieldPPIISample? entity { get; set; }
        }

        public class ExportView
        {
            public string Heading { get; set; }
            public string VisibleRole { get; set; }
            public string TableReference { get; set; }
            public string Filter { get; set; }
        }


        public class SurveyViewModel
        {
            public int ID { get; set; }
            public string UID { get; set; } = "";
            public string Name { get; set; } = "";
            public string ShortVersion { get; set; } = "";
            public string ExecutionYear { get; set; } = "";
            public string ReferencePeriod { get; set; } = "";
            public string SurveyType { get; set; } = "";
            public string DatabaseName { get; set; } = "";
            public DateTime CreatedTime { get; set; }
            public DateTime? ModifiedTime { get; set; }
            public bool IsActive { get; set; }
            public bool IsDeleted { get; set; }
            public string CreatedBy { get; set; } = "";
            public string? ModifiedBy { get; set; }

            // Nested list of questionnaires
            public List<QuestionnaireViewModel> Questionnaires { get; set; } = new();

            
        }

        public class QuestionnaireViewModel
        {
            public int ID { get; set; }
            public string UID { get; set; } = "";
            public string Survey { get; set; } = "";
            public string Type { get; set; } = "";
            public double Version { get; set; }
            public bool IsActive { get; set; }
            public bool IsDeleted { get; set; }
            public string CreatedBy { get; set; } = "";
            public string? ModifiedBy { get; set; }

            // Nested list of sections
            public List<SectionViewModel> Sections { get; set; } = new();
        }

        public class SectionViewModel
        {
            public int ID { get; set; }
            public string UID { get; set; } = "";
            public string Questionnaire { get; set; } = "";
            public int SortOrder { get; set; }
            public string Title { get; set; } = "";
            public string Subtitle { get; set; } = "";
            public bool IsVisible { get; set; }
            public bool IsDeleted { get; set; }
            public string CreatedBy { get; set; } = "";
            public string? ModifiedBy { get; set; }

            // Nested list of forms
            public List<FormViewModel> Forms { get; set; } = new();
        }

        public class FormViewModel
        {
            public int ID { get; set; }
            public string UID { get; set; } = "";
            public string Section { get; set; } = "";
            public int SortOrder { get; set; }
            public string Heading { get; set; } = "";
            public string Type { get; set; } = "";
            public string TableName { get; set; } = "";
            public bool IsActive { get; set; }
            public bool IsDeleted { get; set; }
            public string CreatedBy { get; set; } = "";
            public string? ModifiedBy { get; set; }
        }

        public class SurveyFormModel
        {
            // Survey
            public string SurveyUID { get; set; }
            public string ShortVersion { get; set; }
            public string Name { get; set; }

            // Questionnaire
            public string Questionnaire { get; set; }

            // Section
            public int SectionNo { get; set; }
            public string Title { get; set; }
            public string SubTitle { get; set; }

            // Form
            public string UID { get; set; }
            public int FormNo { get; set; }
            public string Heading { get; set; }
            public string FormType { get; set; }
        }

        public class FormNextOrPreviousModel
        {
            
            public int? NextSectionNo { get; set; }
            public int? PreviousSectionNo { get; set; }

            public int? NextFormNo { get; set; }
            public int? PreviousFormNo { get; set; }

            public string? NextFormUID { get; set; }
            public string? PreviousFormUID { get; set; }
        }

        public class AreaViewModel
        {
            public string? AreaCode { get; set; }
            public string? Name { get; set; }
            public string? Address { get; set; }
            public string? Contact { get; set; }
            public string? Remarks { get; set; }
        }
    }
}
