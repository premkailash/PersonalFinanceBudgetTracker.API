using PersonalFinanceBudgetTrackerAPI.Models.Dtos.Auth;

namespace PersonalFinanceBudgetTrackerAPI.Repository.Auth
{
    public interface IAuthService
    {
        Task<AuthResult> RegisterAsync(RegisterRequestDto request);
        Task<AuthResult> LoginAsync(LoginRequestDto request);
        Task<AuthResult> LogoutAsync(int userId);
    }
}
