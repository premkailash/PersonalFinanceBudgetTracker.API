using PersonalFinanceBudgetTrackerAPI.Models.Dtos.Transaction;

namespace PersonalFinanceBudgetTrackerAPI.Repository.Transaction
{
    public interface ITransactionService
    {
        Task<TransactionListResult> GetTransactionsAsync(
           int accountId, DateTime from, DateTime to, int callerId);

        Task<TransactionResult> GetTransactionByIdAsync(
            int transactionId, int callerId);

        Task<TransactionResult> CreateTransactionAsync(
            CreateTransactionRequestDto request, int callerId);

        Task<TransactionResult> UpdateTransactionAsync(
            UpdateTransactionRequestDto request, int callerId);

        Task<TransactionResult> DeleteTransactionAsync(
            int transactionId, int callerId);
    }
}
