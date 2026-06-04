using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using PersonalFinanceBudgetTrackerAPI.Context;

namespace PersonalFinanceBudgetTrackerAPI.Repository.HealthCheck
{
    /// <summary>
    /// Verifies that the API can open a connection to PostgreSQL and execute
    /// a lightweight query.
    ///
    /// This check is registered with the tag "db" so the health-check middleware
    /// can surface it independently from the composite /health/ready response.
    ///
    /// A single SELECT 1 is issued via ExecuteSqlRawAsync so EF Core does not
    /// pull any entity rows — it only proves that the connection pool can reach
    /// the database server and that the ECS task's network path is open.
    /// </summary>
    public class DatabaseHealthCheck : IHealthCheck
    {
        private readonly AppDbContext _db;

        public DatabaseHealthCheck(AppDbContext db)
        {
            _db = db;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // ExecuteSqlRaw with a trivial query is the lowest-overhead way
                // to verify the connection without touching any real table.
                await _db.Database
                    .ExecuteSqlRawAsync("SELECT 1", cancellationToken);

                return HealthCheckResult.Healthy(
                    "PostgreSQL connection is healthy.",
                    new Dictionary<string, object>
                    {
                        ["database"] = _db.Database.GetDbConnection().Database,
                        ["provider"] = "Npgsql (PostgreSQL)"
                    });
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy(
                    "PostgreSQL connection failed.",
                    exception: ex,
                    data: new Dictionary<string, object>
                    {
                        ["error"] = ex.Message,
                        ["database"] = _db.Database.GetDbConnection().Database
                    });
            }
        }
    }

}
