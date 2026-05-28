namespace PersonalFinanceBudgetTrackerAPI.Repository.SavingsGoal
{
    public interface ISavingsGoalAlertService
    {
        /// <summary>
        /// Called after a goal's CurrentAmount or AutoContributeAmount has been
        /// written to the database.  Inspects progress and sends threshold
        /// notifications as needed.
        /// </summary>
        /// <param name="goal">
        /// The fully-loaded <see cref="SavingsGoal"/> entity as it exists in
        /// the database after the save (navigation properties not required).
        /// </param>

        Task EvaluateAndNotifyAsync(Models.Entity.SavingsGoal goal);
    }
}
