namespace PersonalFinanceBudgetTrackerAPI.Repository.User
{
    public interface ITokenBlacklist
    {
        Task InvalidateUserTokensAsync(int userId);
        Task<bool> IsUserInvalidatedAsync(int userId, long tokenIssuedAt);

    }
}
