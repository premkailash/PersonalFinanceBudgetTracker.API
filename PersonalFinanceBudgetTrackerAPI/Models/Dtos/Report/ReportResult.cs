namespace PersonalFinanceBudgetTrackerAPI.Models.Dtos.Report
{
    public class ReportResult<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }
    }

}
