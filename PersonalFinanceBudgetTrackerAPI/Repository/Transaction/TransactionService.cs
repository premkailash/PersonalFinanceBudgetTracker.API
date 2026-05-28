using PersonalFinanceBudgetTrackerAPI.Context;
using PersonalFinanceBudgetTrackerAPI.Models.Dtos.Transaction;
using Microsoft.EntityFrameworkCore;
using PersonalFinanceBudgetTrackerAPI.Repository.Budget;

namespace PersonalFinanceBudgetTrackerAPI.Repository.Transaction
{
    public class TransactionService : ITransactionService
    {
        private readonly AppDbContext _db;
        private readonly IBudgetAlertService _alertService;
        public TransactionService(AppDbContext db, IBudgetAlertService alertService)
        {
            _db = db;
            _alertService = alertService;
        }

        // ---------------------------------------------------------------
        // GET TRANSACTIONS WITH FILTERS
        // ---------------------------------------------------------------
        public async Task<TransactionListResult> GetTransactionsAsync(
            int accountId, DateTime from, DateTime to, int callerId)
        {
            // Verify account belongs to caller
            var account = await _db.Accounts
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.AccountId == accountId && a.IsActive);

            if (account == null)
                return new TransactionListResult
                {
                    Success = false,
                    NotFound = true,
                    Message = $"Account with ID {accountId} was not found or is inactive."
                };

            if (account.UserId != callerId)
                return new TransactionListResult
                {
                    Success = false,
                    NotFound = false,
                    Message = "You are not authorized to access transactions for this account."
                };

            var transactions = await _db.Transactions
                .AsNoTracking()
                .Include(t => t.Account)
                .Include(t => t.Category)
                .Where(t => t.AccountId == accountId
                         && t.TransactionDate >= from.Date
                         && t.TransactionDate <= to.Date)
                .OrderByDescending(t => t.TransactionDate)
                .Select(t => MapToDto(t))
                .ToListAsync();

            return new TransactionListResult
            {
                Success = true,
                Message = $"{transactions.Count} transaction(s) found.",
                Data = transactions
            };
        }

        // ---------------------------------------------------------------
        // GET TRANSACTION BY ID
        // ---------------------------------------------------------------
        public async Task<TransactionResult> GetTransactionByIdAsync(
            int transactionId, int callerId)
        {
            var transaction = await _db.Transactions
                .AsNoTracking()
                .Include(t => t.Account)
                .Include(t => t.Category)
                .FirstOrDefaultAsync(t => t.TransactionId == transactionId);

            if (transaction == null)
                return new TransactionResult
                {
                    Success = false,
                    NotFound = true,
                    Message = $"Transaction with ID {transactionId} was not found."
                };

            if (transaction.UserId != callerId)
                return new TransactionResult
                {
                    Success = false,
                    NotFound = false,
                    Message = "You are not authorized to access this transaction."
                };

            return new TransactionResult { Success = true, Data = MapToDto(transaction) };
        }

        // ---------------------------------------------------------------
        // CREATE TRANSACTION  + update budget CurrentAmount
        // ---------------------------------------------------------------
        public async Task<TransactionResult> CreateTransactionAsync(
            CreateTransactionRequestDto request, int callerId)
        {
            var account = await _db.Accounts
                .FirstOrDefaultAsync(a => a.AccountId == request.AccountId && a.IsActive);

            if (account == null)
                return new TransactionResult
                {
                    Success = false,
                    NotFound = true,
                    Message = $"Account with ID {request.AccountId} was not found or is inactive."
                };

            if (account.UserId != callerId)
                return new TransactionResult
                {
                    Success = false,
                    NotFound = false,
                    Message = "You are not authorized to add transactions to this account."
                };

            var transaction = new Models.Entity.Transaction
            {
                AccountId = request.AccountId,
                UserId = callerId,
                Amount = request.Amount,
                Currency = request.Currency,
                Type = request.Type,
                CategoryId = request.CategoryId,
                Description = request.Description,
                TransactionDate = request.TransactionDate,
                IsRecurring = request.IsRecurring,
                CreatedAt = DateTime.UtcNow
            };

            _db.Transactions.Add(transaction);
            await _db.SaveChangesAsync();

            // Update budget: CurrentAmount += transaction.Amount
            await ApplyBudgetDeltaAsync(
                callerId,
                request.CategoryId,
                request.TransactionDate,
                +request.Amount);

            // Reload navigation properties for response
            await _db.Entry(transaction).Reference(t => t.Account).LoadAsync();
            await _db.Entry(transaction).Reference(t => t.Category).LoadAsync();

            return new TransactionResult
            {
                Success = true,
                Message = $"Transaction {transaction.TransactionId} created successfully.",
                Data = MapToDto(transaction)
            };
        }

        // ---------------------------------------------------------------
        // UPDATE TRANSACTION
        // 1. Reverse old budget impact
        // 2. Apply new values
        // 3. Re-apply new budget impact
        // ---------------------------------------------------------------
        public async Task<TransactionResult> UpdateTransactionAsync(
            UpdateTransactionRequestDto request, int callerId)
        {
            var transaction = await _db.Transactions
                .Include(t => t.Account)
                .Include(t => t.Category)
                .FirstOrDefaultAsync(t => t.TransactionId == request.TransactionId);

            if (transaction == null)
                return new TransactionResult
                {
                    Success = false,
                    NotFound = true,
                    Message = $"Transaction with ID {request.TransactionId} was not found."
                };

            if (transaction.UserId != callerId)
                return new TransactionResult
                {
                    Success = false,
                    NotFound = false,
                    Message = "You are not authorized to update this transaction."
                };

            // Step 1 — reverse old budget impact: CurrentAmount -= oldAmount
            await ApplyBudgetDeltaAsync(
                callerId,
                transaction.CategoryId,
                transaction.TransactionDate,
                -transaction.Amount);

            // Step 2 — apply updated fields
            transaction.Amount = request.Amount;
            transaction.Description = request.Description;
            transaction.TransactionDate = request.TransactionDate;

            await _db.SaveChangesAsync();

            // Step 3 — apply new budget impact: CurrentAmount += newAmount
            await ApplyBudgetDeltaAsync(
                callerId,
                transaction.CategoryId,
                request.TransactionDate,
                +request.Amount);

            return new TransactionResult
            {
                Success = true,
                Message = $"Transaction {transaction.TransactionId} updated successfully.",
                Data = MapToDto(transaction)
            };
        }

        // ---------------------------------------------------------------
        // DELETE TRANSACTION
        // 1. Reverse budget impact
        // 2. Delete the record
        // ---------------------------------------------------------------
        public async Task<TransactionResult> DeleteTransactionAsync(
            int transactionId, int callerId)
        {
            var transaction = await _db.Transactions
                .FirstOrDefaultAsync(t => t.TransactionId == transactionId);

            if (transaction == null)
                return new TransactionResult
                {
                    Success = false,
                    NotFound = true,
                    Message = $"Transaction with ID {transactionId} was not found."
                };

            if (transaction.UserId != callerId)
                return new TransactionResult
                {
                    Success = false,
                    NotFound = false,
                    Message = "You are not authorized to delete this transaction."
                };

            // Reverse budget: CurrentAmount -= amount
            await ApplyBudgetDeltaAsync(
                callerId,
                transaction.CategoryId,
                transaction.TransactionDate,
                -transaction.Amount);

            _db.Transactions.Remove(transaction);
            await _db.SaveChangesAsync();

            return new TransactionResult
            {
                Success = true,
                Message = $"Transaction {transactionId} deleted successfully."
            };
        }

        // ---------------------------------------------------------------
        // Budget delta helper
        // Finds the budget for (userId, categoryId) whose TargetDate falls
        // in the same year-month as the transaction date and applies delta.
        // ---------------------------------------------------------------
        private async Task ApplyBudgetDeltaAsync(
            int userId,
            int categoryId,
            DateTime transactionDate,
            decimal delta)
        {
            var budget = await _db.Budgets.FirstOrDefaultAsync(b =>
                b.UserId == userId &&
                b.CategoryId == categoryId &&
                b.TargetDate.Year == transactionDate.Year &&
                b.TargetDate.Month == transactionDate.Month);

            if (budget == null) return;   // No matching budget — nothing to update

            budget.CurrentAmount = Math.Max(0, budget.CurrentAmount + delta);
            await _db.SaveChangesAsync();

            // ── Budget-alert check ───────────────────────────────────────────
            // Only evaluate when delta is positive (amount being added).
            // A negative delta (reversal on update/delete) can never cross a
            // threshold upward, so no notification is warranted.
            if (delta > 0)
                await _alertService.EvaluateAndNotifyAsync(budget);

        }

        // ---------------------------------------------------------------
        // DTO mapper
        // ---------------------------------------------------------------
        private static TransactionResponseDto MapToDto(Models.Entity.Transaction t) =>
            new TransactionResponseDto
            {
                TransactionId = t.TransactionId,
                AccountId = t.AccountId,
                AccountName = t.Account?.AccountName ?? string.Empty,
                Amount = t.Amount,
                Currency = t.Currency,
                Type = t.Type,
                CategoryId = t.CategoryId,
                CategoryName = t.Category?.Name ?? string.Empty,
                Description = t.Description,
                TransactionDate = t.TransactionDate,
                IsRecurring = t.IsRecurring,
                CreatedAt = t.CreatedAt
            };
    }

}
