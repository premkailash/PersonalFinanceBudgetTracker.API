using PersonalFinanceBudgetTrackerAPI.Context;
using PersonalFinanceBudgetTrackerAPI.Models.Dtos.User;
using Microsoft.EntityFrameworkCore;

namespace PersonalFinanceBudgetTrackerAPI.Repository.User
{
    public class UserService : IUserService
    {
        private readonly AppDbContext _db;

        public UserService(AppDbContext db)
        {
            _db = db;
        }

        // ---------------------------------------------------------------
        // GET ALL USERS (Admin only)
        // ---------------------------------------------------------------
        public async Task<UserListResult> GetAllUsersAsync()
        {
            try
            {
                var users = await _db.Users
                    .AsNoTracking()
                    .OrderBy(u => u.CreatedAt)
                    .Select(u => new UserResponseDto
                    {
                        UserId = u.UserId,
                        Username = u.Username,
                        Email = u.Email,
                        Is2FAEnabled = u.Is2FAEnabled,
                        CreatedAt = u.CreatedAt
                    })
                    .ToListAsync();

                return new UserListResult
                {
                    Success = true,
                    Message = $"{users.Count} user(s) retrieved successfully.",
                    Data = users
                };
            }
            catch (Exception ex)
            {
                return new UserListResult
                {
                    Success = false,
                    Message = $"An error occurred while retrieving users: {ex.Message}"
                };
            }
        }

        // ---------------------------------------------------------------
        // GET USER BY ID (Admin or User — guarded at controller level)
        // ---------------------------------------------------------------
        public async Task<UserResult> GetUserByIdAsync(int userId)
        {
            var user = await _db.Users
                .AsNoTracking()
                .Where(u => u.UserId == userId)
                .Select(u => new UserResponseDto
                {
                    UserId = u.UserId,
                    Username = u.Username,
                    Email = u.Email,
                    Is2FAEnabled = u.Is2FAEnabled,
                    CreatedAt = u.CreatedAt
                })
                .FirstOrDefaultAsync();

            if (user == null)
                return new UserResult
                {
                    Success = false,
                    Message = $"User with ID {userId} was not found."
                };

            return new UserResult
            {
                Success = true,
                Message = "User retrieved successfully.",
                Data = user
            };
        }

        // ---------------------------------------------------------------
        // UPDATE USER (User role — own profile only, guarded at controller)
        // ---------------------------------------------------------------
        public async Task<UserResult> UpdateUserAsync(UpdateUserRequestDto request)
        {
            var user = await _db.Users.FindAsync(request.UserId);

            if (user == null)
                return new UserResult
                {
                    Success = false,
                    Message = $"User with ID {request.UserId} was not found."
                };

            // Check if the new username is already taken by another user
            bool usernameTaken = await _db.Users
                .AnyAsync(u => u.Username.ToLower() == request.Username.ToLower()
                            && u.UserId != request.UserId);

            if (usernameTaken)
                return new UserResult
                {
                    Success = false,
                    Message = $"Username '{request.Username}' is already taken. Please choose a different username."
                };

            // Apply updates
            user.Username = request.Username;
            user.Is2FAEnabled = request.Is2FAEnabled;

            await _db.SaveChangesAsync();

            return new UserResult
            {
                Success = true,
                Message = "User profile updated successfully.",
                Data = new UserResponseDto
                {
                    UserId = user.UserId,
                    Username = user.Username,
                    Email = user.Email,
                    Is2FAEnabled = user.Is2FAEnabled,
                    CreatedAt = user.CreatedAt
                }
            };
        }

        // ---------------------------------------------------------------
        // DELETE USER (Admin only)
        // ---------------------------------------------------------------
        public async Task<UserResult> DeleteUserAsync(int userId)
        {
            var user = await _db.Users.FindAsync(userId);

            if (user == null)
                return new UserResult
                {
                    Success = false,
                    Message = $"User with ID {userId} was not found."
                };

            _db.Users.Remove(user);
            await _db.SaveChangesAsync();

            return new UserResult
            {
                Success = true,
                Message = $"User '{user.Username}' (ID: {userId}) has been successfully deleted."
            };
        }
    }

}
