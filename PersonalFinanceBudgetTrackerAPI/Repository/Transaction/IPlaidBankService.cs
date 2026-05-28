using PersonalFinanceBudgetTrackerAPI.Models.Dtos.Transaction;

namespace PersonalFinanceBudgetTrackerAPI.Repository.Transaction
{
    public interface IPlaidBankService
    {
        Task<IEnumerable<ImportedTransactionDto>> FetchTransactionsAsync(Models.Entity.Account account);

    }
}
