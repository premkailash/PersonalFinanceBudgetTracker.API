using PersonalFinanceBudgetTrackerAPI.Models.Dtos.SavingsGoal;

namespace PersonalFinanceBudgetTrackerAPI.Repository.SavingsGoal
{
    public interface ISavingsGoalService
    {
        Task<SavingsGoalListResult> GetAllGoalsAsync(int userId);
        Task<SavingsGoalResult> GetGoalByIdAsync(int goalId, int callerId);
        Task<SavingsGoalResult> CreateGoalAsync(CreateSavingsGoalRequestDto request);
        Task<SavingsGoalResult> UpdateGoalAsync(UpdateSavingsGoalRequestDto request, int callerId);
        Task<SavingsGoalResult> DeleteGoalAsync(int goalId, int callerId);
        Task<SavingsGoalResult> ContributeAsync(ContributeRequestDto request, int callerId);

    }
}
