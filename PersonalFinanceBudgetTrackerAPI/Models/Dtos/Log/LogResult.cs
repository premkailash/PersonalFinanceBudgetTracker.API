namespace PersonalFinanceBudgetTrackerAPI.Models.Dtos.Log
{
    public class LogResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool NotFound { get; set; }
        public LogResponseDto? Data { get; set; }

    }
}
