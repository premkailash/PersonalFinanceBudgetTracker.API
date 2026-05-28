namespace PersonalFinanceBudgetTrackerAPI.Models.Dtos.Log
{
    public class LogResponseDto
    {
        public int LogId { get; set; }
        public string Event { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;
        public int? ActorId { get; set; }
        public string? Username { get; set; }   // joined from Users table
        public DateTime Timestamp { get; set; }

    }
}
