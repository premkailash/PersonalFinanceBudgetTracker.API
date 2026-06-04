using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace PersonalFinanceBudgetTrackerAPI.Controllers
{
    [ApiController]
    [Route("health")]
    [AllowAnonymous]
    public class HealthController : ControllerBase
    {
        private readonly HealthCheckService _healthCheckService;

        // Static assembly metadata — computed once at startup
        private static readonly string _version =
            Assembly.GetExecutingAssembly()
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                    ?.InformationalVersion
            ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
            ?? "unknown";

        private static readonly string _appName =
            Assembly.GetExecutingAssembly().GetName().Name ?? "FinanceApp.API";

        public HealthController(HealthCheckService healthCheckService)
        {
            _healthCheckService = healthCheckService;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  GET /health/live
        //  Liveness probe — is the .NET process running and responsive?
        //  Returns 200 OK immediately without touching external resources.
        //  Configure this path on the ALB Target Group health check.
        // ─────────────────────────────────────────────────────────────────────
        [HttpGet("live")]
        [DisableRateLimiting]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult Liveness()
        {
            return Ok(new
            {
                status = "Healthy",
                app = _appName,
                version = _version,
                timestamp = DateTime.UtcNow.ToString("o"),
                uptime = GetUptime()
            });
        }

        // ─────────────────────────────────────────────────────────────────────
        //  GET /health/ready
        //  Readiness probe — can the app serve real traffic right now?
        //  Runs all registered IHealthCheck implementations (e.g. database).
        //  HTTP 200 = Healthy or Degraded (partial outage — still routing traffic)
        //  HTTP 503 = Unhealthy (take instance out of rotation)
        // ─────────────────────────────────────────────────────────────────────
        [HttpGet("ready")]
        [DisableRateLimiting]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> Readiness(CancellationToken cancellationToken)
        {
            var report = await _healthCheckService.CheckHealthAsync(cancellationToken);

            var response = new
            {
                status = report.Status.ToString(),
                app = _appName,
                version = _version,
                timestamp = DateTime.UtcNow.ToString("o"),
                duration = report.TotalDuration.TotalMilliseconds,
                checks = report.Entries.Select(e => new
                {
                    name = e.Key,
                    status = e.Value.Status.ToString(),
                    description = e.Value.Description,
                    duration = e.Value.Duration.TotalMilliseconds,
                    data = e.Value.Data.Count > 0 ? e.Value.Data : null,
                    error = e.Value.Exception?.Message
                })
            };

            // 200 for Healthy and Degraded — only 503 for Unhealthy
            // ALB will stop routing to instances returning 503.
            return report.Status == HealthStatus.Unhealthy
                ? StatusCode(StatusCodes.Status503ServiceUnavailable, response)
                : Ok(response);
        }
        // ─────────────────────────────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────────────────────────────
        private static readonly DateTime _startTime = DateTime.UtcNow;

        private static string GetUptime()
        {
            var uptime = DateTime.UtcNow - _startTime;
            return $"{(int)uptime.TotalHours:D2}:{uptime.Minutes:D2}:{uptime.Seconds:D2}";
        }
    }

}
