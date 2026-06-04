namespace PersonalFinanceBudgetTrackerAPI.Extensions
{
    /// <summary>
    /// Central registry of all rate-limit policy names.
    /// Reference these constants in attributes and middleware — never use
    /// magic strings scattered across the project.
    /// </summary>
    public static class RateLimitPolicies
    {
        /// <summary>
        /// Applied to /api/auth/* endpoints.
        /// Limit: 10 requests per 60-second window.
        /// Partitioned by: JWT subject (authenticated) or client IP (unauthenticated).
        /// </summary>
        public const string Auth = "auth_fixed_window";

        /// <summary>
        /// Applied to all other /api/* endpoints (data plane).
        /// Limit: 300 requests per 60-second window.
        /// Partitioned by: JWT subject (authenticated) or client IP (unauthenticated).
        /// </summary>
        public const string Data = "data_fixed_window";
    }

}
