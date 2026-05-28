using PersonalFinanceBudgetTrackerAPI.Models.Dtos.Transaction;

namespace PersonalFinanceBudgetTrackerAPI.Repository.Transaction
{
    public class PlaidBankService : IPlaidBankService
    {
        private readonly ILogger<PlaidBankService> _logger;
        public PlaidBankService(ILogger<PlaidBankService> logger)
        {
            _logger = logger;
        }

        public Task<IEnumerable<ImportedTransactionDto>> FetchTransactionsAsync(Models.Entity.Account account)
        {
            _logger.LogWarning(
               "PlaidBankServiceStub.FetchTransactionsAsync called for account {AccountId} " +
               "({AccountName}). No real bank API call made — returning empty list. " +
               "Register a real IPlaidBankService implementation for production.",
               account.AccountId,
               account.AccountName);

            // Return an empty set so the import orchestrator can still run
            // without throwing, and the system logs a clean audit entry.
            return Task.FromResult(Enumerable.Empty<ImportedTransactionDto>());

        }
    }
}
