namespace PersonalFinanceBudgetTrackerAPI.Models.Dtos.DataExport
{
    public class DataExportResponseDto
    {
        public int ExportId { get; set; }
        public string ReportType { get; set; } = string.Empty;
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public int UserId { get; set; }
        public int AccountId { get; set; }
        public bool IsGenerated { get; set; }
        public string ReportOptions { get; set; } = string.Empty;
        public string? ReportLink { get; set; }
        public DateTime Timestamp { get; set; }

    }
}
