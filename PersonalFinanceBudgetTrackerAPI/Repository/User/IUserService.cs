using PersonalFinanceBudgetTrackerAPI.Models.Dtos.User;

namespace PersonalFinanceBudgetTrackerAPI.Repository.User
{
    public interface IUserService
    {
        Task<UserListResult> GetAllUsersAsync();
        Task<UserResult> GetUserByIdAsync(int userId);
        Task<UserResult> UpdateUserAsync(UpdateUserRequestDto request);
        Task<UserResult> DeleteUserAsync(int userId);

    }
}
