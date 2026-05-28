using PersonalFinanceBudgetTrackerAPI.Models.Dtos.Transaction;

namespace PersonalFinanceBudgetTrackerAPI.Repository.Transaction
{
    public interface ITransactionImportService
    {
        Task<ImportResult> ImportAllLinkedAccountsAsync();
    }
}
