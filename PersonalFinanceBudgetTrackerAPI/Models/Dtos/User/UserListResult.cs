namespace PersonalFinanceBudgetTrackerAPI.Models.Dtos.User
{
    public class UserListResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public IEnumerable<UserResponseDto>? Data { get; set; }

    }
}
