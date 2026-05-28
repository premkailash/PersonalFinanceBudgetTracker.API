using PersonalFinanceBudgetTrackerAPI.Models.Dtos.Log;

namespace PersonalFinanceBudgetTrackerAPI.Repository.Log
{
    public interface ILogService
    {
        Task<LogListResult> GetAllLogsAsync();
        Task<LogResult> GetLogByIdAsync(int logId);
        Task<LogResult> CreateLogAsync(CreateLogRequestDto request);

    }
}
