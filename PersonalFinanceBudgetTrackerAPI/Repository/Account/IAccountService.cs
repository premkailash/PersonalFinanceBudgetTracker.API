using PersonalFinanceBudgetTrackerAPI.Models.Dtos.Account;

namespace PersonalFinanceBudgetTrackerAPI.Repository.Account
{
    public interface IAccountService
    {
        Task<AccountListResult> GetAllAccountsAsync(int userId);
        Task<AccountResult> GetAccountByIdAsync(int accountId, int callerId);
        Task<AccountResult> CreateAccountAsync(CreateAccountRequestDto request);
        Task<AccountResult> UpdateAccountAsync(UpdateAccountRequestDto request, int callerId);
        Task<AccountResult> UnlinkAccountAsync(int accountId, int callerId);
        Task<AccountCountResult> GetAccountCountAsync();

    }
}
