namespace PBS.ERP.Shared.Models
{
    public sealed class SurveyDatabasePlan
    {
        public bool Success { get; set; }
        public bool IsApplicable { get; set; }
        public string Message { get; set; } = "";

        public string? TableUid { get; set; }
        public string? RecordUid { get; set; }

        public string? OldDatabaseName { get; set; }
        public string? NewDatabaseName { get; set; }

        public static SurveyDatabasePlan NotApplicable()
        {
            return new SurveyDatabasePlan
            {
                Success = true,
                IsApplicable = false,
                Message = "Not a Survey table."
            };
        }

        public static SurveyDatabasePlan Fail(string message)
        {
            return new SurveyDatabasePlan
            {
                Success = false,
                IsApplicable = false,
                Message = message
            };
        }
    }
}
