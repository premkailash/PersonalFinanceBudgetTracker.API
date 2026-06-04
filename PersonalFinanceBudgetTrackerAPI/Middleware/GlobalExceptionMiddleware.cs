using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Text.Json;

namespace PersonalFinanceBudgetTrackerAPI.Middleware
{
    /// <summary>
    /// Global exception-handling middleware.
    ///
    /// Why a middleware instead of UseExceptionHandler or ProblemDetails service?
    /// ────────────────────────────────────────────────────────────────────────────
    /// On ECS Fargate the container's stdout/stderr IS the log stream that
    /// CloudWatch Logs ingests.  Unhandled exceptions that bubble up past
    /// UseExceptionHandler are sometimes swallowed silently depending on the
    /// Kestrel version and the exception type (e.g. OperationCanceledException
    /// from client disconnects, TaskCanceledException from 30-second timeouts).
    /// A hand-written middleware gives full control over:
    ///   • Which exceptions are logged at Error vs Warning
    ///   • What the JSON response body looks like (RFC 7807 ProblemDetails)
    ///   • Whether stack traces appear in responses (never in Production)
    ///   • What correlation ID is emitted so CloudWatch Insights can join logs
    ///
    /// Middleware position in the pipeline
    /// ─────────────────────────────────────
    ///   MUST be the FIRST middleware registered so it wraps every subsequent
    ///   middleware and controller.  Add it immediately after builder.Build().
    ///
    /// Exception-to-status-code mapping
    /// ──────────────────────────────────
    ///   ArgumentException / ValidationException    → 400 Bad Request
    ///   UnauthorizedAccessException                → 401 Unauthorized
    ///   KeyNotFoundException                       → 404 Not Found
    ///   InvalidOperationException                  → 409 Conflict
    ///   OperationCanceledException /
    ///   TaskCanceledException                      → 499 Client Closed Request (logged as Warning)
    ///   TimeoutException                           → 504 Gateway Timeout
    ///   Everything else                            → 500 Internal Server Error
    /// </summary>
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;
        private readonly IHostEnvironment _env;

        // Correlation-ID header — matches AWS ALB X-Amzn-Trace-Id or custom header
        private const string CorrelationHeader = "X-Correlation-Id";

        public GlobalExceptionMiddleware(
            RequestDelegate next,
            ILogger<GlobalExceptionMiddleware> logger,
            IHostEnvironment env)
        {
            _next = next;
            _logger = logger;
            _env = env;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Attach or generate a correlation ID for the entire request lifetime
            string correlationId = GetOrCreateCorrelationId(context);

            // Always echo the correlation ID back in the response header
            context.Response.OnStarting(() =>
            {
                if (!context.Response.Headers.ContainsKey(CorrelationHeader))
                    context.Response.Headers[CorrelationHeader] = correlationId;
                return Task.CompletedTask;
            });

            try
            {
                await _next(context);
            }
            // ── Client disconnected — ECS Fargate generates many of these ────
            catch (OperationCanceledException ex) when (context.RequestAborted.IsCancellationRequested)
            {
                // Client closed the connection before the server finished.
                // Log at Warning (not Error) so CloudWatch alarms are not
                // triggered by normal browser behaviour (tab closed, navigation).
                _logger.LogWarning(
                    "Request cancelled by client. " +
                    "CorrelationId={CorrelationId} Method={Method} Path={Path}",
                    correlationId,
                    context.Request.Method,
                    context.Request.Path);

                // 499 is a nginx convention understood by most APM tools.
                // The response cannot be written if the client has gone, but
                // we set the status code for logging completeness.
                if (!context.Response.HasStarted)
                    context.Response.StatusCode = 499;

                // Do NOT rethrow — this is expected behaviour on Fargate
                // (rolling deploys drain connections, clients reconnect).
            }
            // ── Server-side timeout (e.g. 30-second transaction fetch) ───────
            catch (TimeoutException ex)
            {
                await HandleExceptionAsync(context, ex, correlationId,
                    HttpStatusCode.GatewayTimeout,
                    "The request timed out. Please try again with a smaller date range.");
            }
            // ── Validation / bad input ────────────────────────────────────────
            catch (ArgumentException ex)
            {
                await HandleExceptionAsync(context, ex, correlationId,
                    HttpStatusCode.BadRequest,
                    ex.Message,
                    logLevel: LogLevel.Warning);
            }
            // ── Not found ─────────────────────────────────────────────────────
            catch (KeyNotFoundException ex)
            {
                await HandleExceptionAsync(context, ex, correlationId,
                    HttpStatusCode.NotFound,
                    ex.Message,
                    logLevel: LogLevel.Warning);
            }
            // ── Ownership / authorisation failure ─────────────────────────────
            catch (UnauthorizedAccessException ex)
            {
                await HandleExceptionAsync(context, ex, correlationId,
                    HttpStatusCode.Unauthorized,
                    "You are not authorised to perform this action.",
                    logLevel: LogLevel.Warning);
            }
            // ── Business-rule violation (e.g. duplicate budget) ───────────────
            catch (InvalidOperationException ex)
            {
                await HandleExceptionAsync(context, ex, correlationId,
                    HttpStatusCode.Conflict,
                    ex.Message,
                    logLevel: LogLevel.Warning);
            }
            // ── EF Core / database errors ──────────────────────────────────────
            catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
            {
                _logger.LogError(ex,
                    "Database update failed. " +
                    "CorrelationId={CorrelationId} Method={Method} Path={Path}",
                    correlationId,
                    context.Request.Method,
                    context.Request.Path);

                await HandleExceptionAsync(context, ex, correlationId,
                    HttpStatusCode.InternalServerError,
                    "A database error occurred. Please try again.");
            }
            // ── Everything else — treat as 500 ────────────────────────────────
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex, correlationId,
                    HttpStatusCode.InternalServerError,
                    "An unexpected error occurred. Please try again later.");
            }
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private async Task HandleExceptionAsync(
            HttpContext context,
            Exception ex,
            string correlationId,
            HttpStatusCode statusCode,
            string userMessage,
            LogLevel logLevel = LogLevel.Error)
        {
            // Log with full context so CloudWatch Logs Insights can filter
            _logger.Log(logLevel, ex,
                "Unhandled exception. " +
                "CorrelationId={CorrelationId} " +
                "StatusCode={StatusCode} " +
                "ExceptionType={ExceptionType} " +
                "Method={Method} " +
                "Path={Path} " +
                "Message={Message}",
                correlationId,
                (int)statusCode,
                ex.GetType().Name,
                context.Request.Method,
                context.Request.Path,
                ex.Message);

            // Cannot write a new response if headers have already been sent
            // (e.g. streaming responses). Log and bail out.
            if (context.Response.HasStarted)
            {
                _logger.LogWarning(
                    "Cannot write error response — headers already sent. " +
                    "CorrelationId={CorrelationId}", correlationId);
                return;
            }

            // RFC 7807 ProblemDetails response
            context.Response.Clear();
            context.Response.StatusCode = (int)statusCode;
            context.Response.ContentType = "application/problem+json";

            var problem = new ProblemDetails
            {
                Status = (int)statusCode,
                Title = GetTitle(statusCode),
                Detail = userMessage,
                Instance = context.Request.Path
            };

            problem.Extensions["correlationId"] = correlationId;
            problem.Extensions["timestamp"] = DateTime.UtcNow.ToString("o");

            // Include stack trace ONLY in Development to avoid leaking internals
            if (_env.IsDevelopment())
            {
                problem.Extensions["exception"] = ex.GetType().FullName;
                problem.Extensions["stackTrace"] = ex.StackTrace;
            }

            var json = JsonSerializer.Serialize(problem, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            });

            await context.Response.WriteAsync(json);
        }

        private static string GetOrCreateCorrelationId(HttpContext context)
        {
            // Prefer AWS X-Ray trace ID injected by the ALB, then fall back to
            // the custom header, then generate a new compact GUID.
            return context.Request.Headers["X-Amzn-Trace-Id"].FirstOrDefault()
                ?? context.Request.Headers[CorrelationHeader].FirstOrDefault()
                ?? Guid.NewGuid().ToString("N")[..16];   // compact 16-char ID
        }

        private static string GetTitle(HttpStatusCode code) => code switch
        {
            HttpStatusCode.BadRequest => "Bad Request",
            HttpStatusCode.Unauthorized => "Unauthorized",
            HttpStatusCode.Forbidden => "Forbidden",
            HttpStatusCode.NotFound => "Not Found",
            HttpStatusCode.Conflict => "Conflict",
            HttpStatusCode.UnprocessableEntity => "Unprocessable Entity",
            HttpStatusCode.TooManyRequests => "Too Many Requests",
            HttpStatusCode.GatewayTimeout => "Gateway Timeout",
            HttpStatusCode.InternalServerError => "Internal Server Error",
            _ => "Error"
        };
    }

}
