namespace PersonalFinanceBudgetTrackerAPI.Models.Dtos.Auth
{
    public class AuthResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Token { get; set; }
        public string? Role { get; set; }
        public int? UserId { get; set; }

        public string? UserName { get; set; }

    }
}
