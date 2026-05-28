namespace PersonalFinanceBudgetTrackerAPI.Models.Dtos.Log
{
    public class LogListResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public IEnumerable<LogResponseDto>? Data { get; set; }

    }
}
