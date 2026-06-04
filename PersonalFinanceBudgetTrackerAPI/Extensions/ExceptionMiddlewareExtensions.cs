using PersonalFinanceBudgetTrackerAPI.Middleware;

namespace PersonalFinanceBudgetTrackerAPI.Extensions
{
    /// <summary>
    /// Extension method so Program.cs reads as a single, clean line:
    ///   app.UseGlobalExceptionHandler();
    /// </summary>
    public static class ExceptionMiddlewareExtensions
    {
        /// <summary>
        /// Adds <see cref="GlobalExceptionMiddleware"/> as the outermost
        /// middleware in the pipeline.
        ///
        /// Call this FIRST after <c>builder.Build()</c> and before any other
        /// <c>app.Use*()</c> calls so that exceptions thrown in any downstream
        /// middleware or controller are caught and formatted consistently.
        /// </summary>
        public static IApplicationBuilder UseGlobalExceptionHandler(
            this IApplicationBuilder app)
        {
            return app.UseMiddleware<GlobalExceptionMiddleware>();
        }
    }

}
