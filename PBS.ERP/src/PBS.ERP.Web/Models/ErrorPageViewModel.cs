namespace PBS.ERP.Web.Models
{
    public class ErrorPageViewModel
    {
        public int StatusCode { get; set; }

        public string Title { get; set; } = "Something went wrong";

        public string Message { get; set; } = "An unexpected error occurred while processing your request.";

        public string? Description { get; set; }

        public string? OriginalPath { get; set; }

        public string? RequestId { get; set; }

        public bool ShowRequestId => !string.IsNullOrWhiteSpace(RequestId);
    }
}
