using System.Security.Claims;
using System.Threading.RateLimiting;

namespace PersonalFinanceBudgetTrackerAPI.Extensions
{
    public static class RateLimitingExtensions
    {
        /// <summary>
        /// Registers both fixed-window rate-limit policies.
        ///
        /// Partition key resolution (same logic for both policies):
        ///   Authenticated request  → "user:{sub}" where sub is the JWT "userId" claim.
        ///   Unauthenticated request → "ip:{X-Forwarded-For ?? RemoteIpAddress}".
        ///
        /// When the limit is exceeded the middleware returns HTTP 429 with a
        /// Retry-After header set to the remaining window in seconds.
        /// </summary>
        public static IServiceCollection AddFinanceRateLimiting(
            this IServiceCollection services)
        {
            services.AddRateLimiter(options =>
            {
                // ------------------------------------------------------------------
                // Global 429 handler — runs for every rejected request
                // ------------------------------------------------------------------
                options.OnRejected = async (context, cancellationToken) =>
                {
                    context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    context.HttpContext.Response.ContentType = "application/json";

                    // Surface the retry window to the client
                    if (context.Lease.TryGetMetadata(
                            MetadataName.RetryAfter, out var retryAfter))
                    {
                        context.HttpContext.Response.Headers.RetryAfter =
                            ((int)retryAfter.TotalSeconds).ToString();
                    }

                    await context.HttpContext.Response.WriteAsync(
                        "{\"message\":\"Too many requests. Please slow down and try again.\"}",
                        cancellationToken);
                };

                // ------------------------------------------------------------------
                // Policy: Auth  — 10 req / 60 s
                // /api/auth/register, /api/auth/login, /api/auth/logout
                // ------------------------------------------------------------------
                options.AddPolicy(RateLimitPolicies.Auth, httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: ResolvePartitionKey(httpContext),
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 10,
                            Window = TimeSpan.FromMinutes(1),
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                            QueueLimit = 0        // no queuing — reject immediately
                        }));

                // ------------------------------------------------------------------
                // Policy: Data  — 300 req / 60 s
                // All other /api/* endpoints
                // ------------------------------------------------------------------
                options.AddPolicy(RateLimitPolicies.Data, httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: ResolvePartitionKey(httpContext),
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 300,
                            Window = TimeSpan.FromMinutes(1),
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                            QueueLimit = 0
                        }));
            });

            return services;
        }

        // ----------------------------------------------------------------------
        // Partition key resolution
        // Priority: JWT "userId" claim → X-Forwarded-For header → RemoteIpAddress
        // The prefix ("user:" vs "ip:") prevents namespace collisions between
        // authenticated and unauthenticated partitions.
        // ----------------------------------------------------------------------
        private static string ResolvePartitionKey(HttpContext httpContext)
        {
            // Authenticated — use the stable JWT subject claim
            var userId = httpContext.User?.FindFirstValue("userId");
            if (!string.IsNullOrWhiteSpace(userId))
                return $"user:{userId}";

            // Unauthenticated — fall back to client IP
            // Respect X-Forwarded-For when sitting behind a reverse proxy / load balancer
            var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"]
                                          .FirstOrDefault()
                                         ?.Split(',', StringSplitOptions.TrimEntries)
                                          .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(forwardedFor))
                return $"ip:{forwardedFor}";

            return $"ip:{httpContext.Connection.RemoteIpAddress}";
        }
    }

}
