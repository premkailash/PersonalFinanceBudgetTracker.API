namespace PersonalFinanceBudgetTrackerAPI.Models.Dtos.DataExport
{
    public class DataExportDownloadResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool NotFound { get; set; }
        public bool NotReady { get; set; }
        public DataExportDownloadDto? Data { get; set; }

    }
}
