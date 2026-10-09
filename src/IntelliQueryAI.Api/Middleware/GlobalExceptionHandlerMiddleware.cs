using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IntelliQueryAI.Api.Middleware;

/// <summary>
/// Catches unhandled technical exceptions and serializes a standard RFC 7807 ProblemDetails response.
/// </summary>
public sealed class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public GlobalExceptionHandlerMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlerMiddleware> logger,
        IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred during request execution: {Message}", ex.Message);

            if (ex is BadHttpRequestException badRequestEx)
            {
                context.Response.StatusCode = badRequestEx.StatusCode;
                context.Response.ContentType = "application/problem+json";

                var badRequestProblem = new ProblemDetails
                {
                    Status = badRequestEx.StatusCode,
                    Title = "Request.Invalid",
                    Detail = badRequestEx.Message,
                    Instance = context.Request.Path
                };

                await context.Response.WriteAsJsonAsync(badRequestProblem);
                return;
            }

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/problem+json";

            var errorMessage = $"{ex.Message}{(ex.InnerException != null ? $": {ex.InnerException.Message}" : string.Empty)}";

            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Server.InternalError",
                Detail = errorMessage, //_env.IsDevelopment()
                   //// ? ex.Message?
                   // : "An unexpected error occurred. Please contact platform support.",
                Instance = context.Request.Path
            };

            await context.Response.WriteAsJsonAsync(problem);
        }
    }
}
