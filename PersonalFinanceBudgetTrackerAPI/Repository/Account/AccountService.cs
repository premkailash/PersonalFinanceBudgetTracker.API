using PersonalFinanceBudgetTrackerAPI.Context;
using PersonalFinanceBudgetTrackerAPI.Models.Dtos.Account;
using PersonalFinanceBudgetTrackerAPI.Models.Entity;
using Microsoft.EntityFrameworkCore;

namespace PersonalFinanceBudgetTrackerAPI.Repository.Account
{
    public class AccountService : IAccountService
    {
        private readonly AppDbContext _db;

        public AccountService(AppDbContext db)
        {
            _db = db;
        }

        // ---------------------------------------------------------------
        // GET ALL ACTIVE ACCOUNTS FOR USER
        // ---------------------------------------------------------------
        public async Task<AccountListResult> GetAllAccountsAsync(int userId)
        {
            try
            {
                // Verify the user exists
                bool userExists = await _db.Users.AnyAsync(u => u.UserId == userId);
                if (!userExists)
                    return new AccountListResult
                    {
                        Success = false,
                        Message = $"User with ID {userId} was not found."
                    };

                var accounts = await _db.Accounts
                    .AsNoTracking()
                    .Where(a => a.UserId == userId && a.IsActive)
                    .OrderBy(a => a.LinkedAt)
                    .Select(a => new AccountResponseDto
                    {
                        AccountId = a.AccountId,
                        UserId = a.UserId,
                        AccountName = a.AccountName,
                        AccountType = a.AccountType,
                        Currency = a.Currency,
                        Balance = a.Balance,
                        LinkedAt = a.LinkedAt
                    })
                    .ToListAsync();

                return new AccountListResult
                {
                    Success = true,
                    Message = $"{accounts.Count} active account(s) retrieved successfully.",
                    Data = accounts
                };
            }
            catch (Exception ex)
            {
                return new AccountListResult
                {
                    Success = false,
                    Message = $"An error occurred while retrieving accounts: {ex.Message}"
                };
            }
        }

        // ---------------------------------------------------------------
        // GET ACCOUNT BY ID
        // ---------------------------------------------------------------
        public async Task<AccountResult> GetAccountByIdAsync(int accountId, int callerId)
        {
            var account = await _db.Accounts
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.AccountId == accountId && a.IsActive);

            if (account == null)
                return new AccountResult
                {
                    Success = false,
                    NotFound = true,
                    Message = $"Account with ID {accountId} was not found or has been unlinked."
                };

            // Ensure the account belongs to the calling user
            if (account.UserId != callerId)
                return new AccountResult
                {
                    Success = false,
                    NotFound = false,
                    Message = "You are not authorized to access this account."
                };

            return new AccountResult
            {
                Success = true,
                Message = "Account retrieved successfully.",
                Data = new AccountResponseDto
                {
                    AccountId = account.AccountId,
                    UserId = account.UserId,
                    AccountName = account.AccountName,
                    AccountType = account.AccountType,
                    Currency = account.Currency,
                    Balance = account.Balance,
                    LinkedAt = account.LinkedAt
                }
            };
        }

        // ---------------------------------------------------------------
        // CREATE (LINK) NEW ACCOUNT
        // ---------------------------------------------------------------
        public async Task<AccountResult> CreateAccountAsync(CreateAccountRequestDto request)
        {
            // Check for duplicate: same user + same account name + same account type
            bool isDuplicate = await _db.Accounts.AnyAsync(a =>
                a.UserId == request.UserId &&
                a.AccountName.ToLower() == request.AccountName.ToLower() &&
                a.AccountType.ToLower() == request.AccountType.ToLower() &&
                a.IsActive);

            if (isDuplicate)
                return new AccountResult
                {
                    Success = false,
                    IsDuplicate = true,
                    Message = $"An active account named '{request.AccountName}' of type '{request.AccountType}' already exists for this user."
                };

            var account = new Models.Entity.Account
            {
                UserId = request.UserId,
                AccountName = request.AccountName,
                AccountType = request.AccountType,
                Currency = request.Currency,
                Balance = request.Balance,
                LinkedAt = request.LinkedAt == default ? DateTime.UtcNow : request.LinkedAt,
                IsActive = true
            };

            _db.Accounts.Add(account);
            await _db.SaveChangesAsync();

            return new AccountResult
            {
                Success = true,
                Message = $"Account '{account.AccountName}' has been successfully linked.",
                Data = new AccountResponseDto
                {
                    AccountId = account.AccountId,
                    UserId = account.UserId,
                    AccountName = account.AccountName,
                    AccountType = account.AccountType,
                    Currency = account.Currency,
                    Balance = account.Balance,
                    LinkedAt = account.LinkedAt
                }
            };
        }

        // ---------------------------------------------------------------
        // UPDATE ACCOUNT
        // ---------------------------------------------------------------
        public async Task<AccountResult> UpdateAccountAsync(UpdateAccountRequestDto request, int callerId)
        {
            var account = await _db.Accounts
                .FirstOrDefaultAsync(a => a.AccountId == request.AccountId && a.IsActive);

            if (account == null)
                return new AccountResult
                {
                    Success = false,
                    NotFound = true,
                    Message = $"Account with ID {request.AccountId} was not found or has been unlinked."
                };

            // Ownership check
            if (account.UserId != callerId)
                return new AccountResult
                {
                    Success = false,
                    NotFound = false,
                    Message = "You are not authorized to update this account."
                };

            // Check for duplicate: same user + same account name + same type, but NOT this account
            bool isDuplicate = await _db.Accounts.AnyAsync(a =>
                a.UserId == account.UserId &&
                a.AccountName.ToLower() == request.AccountName.ToLower() &&
                a.AccountType.ToLower() == request.AccountType.ToLower() &&
                a.AccountId != request.AccountId &&
                a.IsActive);

            if (isDuplicate)
                return new AccountResult
                {
                    Success = false,
                    IsDuplicate = true,
                    Message = $"Another active account named '{request.AccountName}' of type '{request.AccountType}' already exists for this user."
                };

            // Apply updates
            account.AccountName = request.AccountName;
            account.AccountType = request.AccountType;
            account.Currency = request.Currency;
            account.Balance = request.Balance;

            await _db.SaveChangesAsync();

            return new AccountResult
            {
                Success = true,
                Message = $"Account '{account.AccountName}' has been successfully updated.",
                Data = new AccountResponseDto
                {
                    AccountId = account.AccountId,
                    UserId = account.UserId,
                    AccountName = account.AccountName,
                    AccountType = account.AccountType,
                    Currency = account.Currency,
                    Balance = account.Balance,
                    LinkedAt = account.LinkedAt
                }
            };
        }

        // ---------------------------------------------------------------
        // UNLINK ACCOUNT (Soft Delete — IsActive = false)
        // ---------------------------------------------------------------
        public async Task<AccountResult> UnlinkAccountAsync(int accountId, int callerId)
        {
            var account = await _db.Accounts
                .FirstOrDefaultAsync(a => a.AccountId == accountId && a.IsActive);

            if (account == null)
                return new AccountResult
                {
                    Success = false,
                    NotFound = true,
                    Message = $"Account with ID {accountId} was not found or has already been unlinked."
                };

            // Ownership check
            if (account.UserId != callerId)
                return new AccountResult
                {
                    Success = false,
                    NotFound = false,
                    Message = "You are not authorized to unlink this account."
                };

            // Soft delete — set IsActive to false
            account.IsActive = false;
            await _db.SaveChangesAsync();

            return new AccountResult
            {
                Success = true,
                Message = $"Account '{account.AccountName}' (ID: {accountId}) has been successfully unlinked."
            };
        }
    }

}
