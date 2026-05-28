namespace PersonalFinanceBudgetTrackerAPI.Models.Dtos.DataExport
{
    public class DataExportResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool NotFound { get; set; }
        public DataExportResponseDto? Data { get; set; }

    }
}
