using PersonalFinanceBudgetTrackerAPI.Context;
using PersonalFinanceBudgetTrackerAPI.Models.Dtos.Log;
using PersonalFinanceBudgetTrackerAPI.Models.Dtos.Transaction;
using Microsoft.EntityFrameworkCore;
using PersonalFinanceBudgetTrackerAPI.Repository.Log;

namespace PersonalFinanceBudgetTrackerAPI.Repository.Transaction
{
    public class TransactionImportService : ITransactionImportService
    {
        private readonly AppDbContext _db;
        private readonly IPlaidBankService _plaid;
        private readonly ILogService _logService;

        public TransactionImportService(
            AppDbContext db,
            IPlaidBankService plaid,
            ILogService logService)
        {
            _db = db;
            _plaid = plaid;
            _logService = logService;
        }

        public async Task<ImportResult> ImportAllLinkedAccountsAsync()
        {
            // Fetch ALL active accounts across ALL users (no userId filter)
            var activeAccounts = await _db.Accounts
                .AsNoTracking()
                .Where(a => a.IsActive)
                .ToListAsync();

            var summary = new ImportResultDto
            {
                TotalAccounts = activeAccounts.Count
            };

            foreach (var account in activeAccounts)
            {
                try
                {
                    // Call Plaid / Open Banking API for this account
                    var rawTransactions = await _plaid.FetchTransactionsAsync(account);

                    foreach (var raw in rawTransactions)
                    {
                        // Idempotency: skip if a transaction with the same account,
                        // date, amount, and type already exists
                        bool exists = await _db.Transactions.AnyAsync(t =>
                            t.AccountId == raw.AccountId &&
                            t.TransactionDate.Date == raw.TransactionDate.Date &&
                            t.Amount == raw.Amount &&
                            t.Type == raw.Type);

                        if (exists)
                        {
                            summary.TotalSkipped++;
                            continue;
                        }

                        var transaction = new Models.Entity.Transaction
                        {
                            AccountId = raw.AccountId,
                            UserId = account.UserId,
                            Amount = raw.Amount,
                            Currency = raw.Currency,
                            Type = raw.Type,
                            CategoryId = raw.CategoryId,
                            Description = raw.Description,
                            TransactionDate = raw.TransactionDate,
                            IsRecurring = raw.IsRecurring,
                            CreatedAt = DateTime.UtcNow
                        };

                        _db.Transactions.Add(transaction);
                        summary.TotalImported++;
                    }

                    await _db.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    summary.Errors.Add(
                        $"Account {account.AccountId} ({account.AccountName}): {ex.Message}");
                }
            }

            // System audit log
            await _logService.CreateLogAsync(new CreateLogRequestDto
            {
                Event = $"Auto-import completed: {summary.TotalImported} imported, " +
                            $"{summary.TotalSkipped} skipped, {summary.Errors.Count} errors " +
                            $"across {summary.TotalAccounts} accounts.",
                EventType = "System",
                UserId = null   // System event — no user actor
            });

            return new ImportResult
            {
                Success = true,
                Message = $"Import complete. {summary.TotalImported} new transaction(s) imported.",
                Data = summary
            };
        }
    }

}
