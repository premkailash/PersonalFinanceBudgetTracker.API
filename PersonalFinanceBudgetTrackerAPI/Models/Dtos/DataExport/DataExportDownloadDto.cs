namespace PersonalFinanceBudgetTrackerAPI.Models.Dtos.DataExport
{
    public class DataExportDownloadDto
    {
        public int ExportId { get; set; }
        public bool IsGenerated { get; set; }
        public string? ReportLink { get; set; }

    }
}
