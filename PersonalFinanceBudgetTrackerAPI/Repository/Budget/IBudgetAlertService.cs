namespace PersonalFinanceBudgetTrackerAPI.Repository.Budget
{
    public interface IBudgetAlertService
    {
        /// <summary>
        /// Called after a budget's CurrentAmount has been written to the DB.
        /// Inspects utilisation and sends threshold notifications as needed.
        /// </summary>
        /// <param name="budget">The fully-loaded budget entity (navigation
        /// properties not required).</param>
        Task EvaluateAndNotifyAsync(Models.Entity.Budget budget);
    }

}
