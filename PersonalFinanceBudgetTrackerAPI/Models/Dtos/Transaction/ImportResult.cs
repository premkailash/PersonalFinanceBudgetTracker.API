namespace PersonalFinanceBudgetTrackerAPI.Models.Dtos.Transaction
{
    public class ImportResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public ImportResultDto? Data { get; set; }

    }
}
