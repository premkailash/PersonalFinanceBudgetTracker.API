namespace PersonalFinanceBudgetTrackerAPI.Models.Dtos.User
{
    public class UserResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public UserResponseDto? Data { get; set; }

    }
}
