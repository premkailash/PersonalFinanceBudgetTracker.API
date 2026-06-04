using Microsoft.Extensions.Diagnostics.HealthChecks;
using PersonalFinanceBudgetTrackerAPI.Repository.HealthCheck;

namespace PersonalFinanceBudgetTrackerAPI.Extensions
{
    public static class HealthCheckExtensions
    {
        /// <summary>
        /// Registers all application health checks with the DI container.
        ///
        /// Add new checks here as the system grows (e.g. Redis, Plaid API, S3).
        /// Each check is tagged so it can be filtered independently:
        ///
        ///   Tag "live"  → fast checks only (no external dependencies)
        ///   Tag "ready" → full readiness checks (DB, external APIs, etc.)
        ///   Tag "db"    → database-specific checks
        /// </summary>

        public static IServiceCollection AddFinanceHealthChecks(
            this IServiceCollection services)
        {
            services
                .AddHealthChecks()

                // ── Database ──────────────────────────────────────────────────
                // Custom check: opens a real connection and runs SELECT 1.
                // Degraded threshold: responds but slowly (> 1 s).
                // Unhealthy threshold: connection refused or exception.
                .Add(new HealthCheckRegistration(
                    name: "database",
                    factory: sp => sp.GetRequiredService<DatabaseHealthCheck>(),
                    failureStatus: HealthStatus.Unhealthy,
                    tags: new[] { "ready", "db" },
                    timeout: TimeSpan.FromSeconds(5)));

            // Register the check class itself so DI can resolve it
            services.AddScoped<DatabaseHealthCheck>();

            return services;
        }

    }
}
