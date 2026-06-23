namespace PersonalFinanceBudgetTrackerAPI.Models.Dtos.DataExport
{
    public class DataExportListResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;        
        public IEnumerable<DataExportResponseDto>? Data { get; set; }
    }
}
