using System.Security.Claims;
using IntelliQueryAI.Api.Extensions;
using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Application.Features.Forecasting.Commands;
using IntelliQueryAI.Application.Features.Forecasting.Dtos;
using IntelliQueryAI.Application.Features.Forecasting.Queries;
using IntelliQueryAI.Domain.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace IntelliQueryAI.Api.Endpoints;

public static class ForecastEndpoints
{
    public static IEndpointRouteBuilder MapForecastEndpoints(this IEndpointRouteBuilder app)
    {
        var groups = new[]
        {
            app.MapGroup("/api/forecasts").WithTags("Statistical Forecasting").RequireAuthorization(),
            app.MapGroup("/forecasts").WithTags("Statistical Forecasting").RequireAuthorization()
        };

        foreach (var group in groups)
        {
            // 1. GET /api/forecasts - List saved statistical forecasts
            group.MapGet("/", async (
                [FromQuery] Guid? workspaceId,
                [FromQuery] string? metricName,
                [FromQuery] int? limit,
                ITenantContext tenantContext,
                [FromServices] IDispatcher dispatcher,
                CancellationToken ct) =>
            {
                Guid? targetWorkspaceId = workspaceId ?? tenantContext.WorkspaceId;
                if (!targetWorkspaceId.HasValue)
                {
                    return Results.BadRequest(new { error = "Workspace ID is required." });
                }

                var query = new GetForecastsQuery(targetWorkspaceId.Value, metricName, limit ?? 50);
                var result = await dispatcher.QueryAsync(query, ct);
                return result.ToHttpResult();
            })
            .WithName($"GetForecasts_{group.GetHashCode()}")
            .WithSummary("Retrieves statistical forecasts for the active workspace.");

            // 2. GET /api/forecasts/{id:guid} - Retrieve specific forecast with full trajectory
            group.MapGet("/{id:guid}", async (
                [FromRoute] Guid id,
                [FromQuery] Guid? workspaceId,
                ITenantContext tenantContext,
                [FromServices] IDispatcher dispatcher,
                CancellationToken ct) =>
            {
                Guid? targetWorkspaceId = workspaceId ?? tenantContext.WorkspaceId;
                if (!targetWorkspaceId.HasValue)
                {
                    return Results.BadRequest(new { error = "Workspace ID is required." });
                }

                var query = new GetForecastByIdQuery(targetWorkspaceId.Value, id);
                var result = await dispatcher.QueryAsync(query, ct);
                return result.ToHttpResult();
            })
            .WithName($"GetForecastById_{group.GetHashCode()}")
            .WithSummary("Retrieves a specific statistical forecast by ID.");

            // 3. POST /api/forecasts/generate - Compute and persist deterministic forecast with AI narrative
            group.MapPost("/generate", async (
                [FromBody] GenerateForecastRequestDto request,
                [FromQuery] Guid? workspaceId,
                ClaimsPrincipal user,
                ITenantContext tenantContext,
                [FromServices] IDispatcher dispatcher,
                CancellationToken ct) =>
            {
                Guid? targetWorkspaceId = workspaceId ?? tenantContext.WorkspaceId;
                if (!targetWorkspaceId.HasValue)
                {
                    return Results.BadRequest(new { error = "Workspace ID is required." });
                }

                var userIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
                Guid? userId = Guid.TryParse(userIdStr, out var parsedUserId) ? parsedUserId : null;

                var command = new GenerateForecastCommand(targetWorkspaceId.Value, userId, request);
                var result = await dispatcher.SendAsync(command, ct);
                return result.ToHttpResult();
            })
            .WithName($"GenerateForecast_{group.GetHashCode()}")
            .WithSummary("Generates, explains, and saves an evidence-backed statistical forecast.");

            // 4. POST /api/forecasts/preview - Fast deterministic calculation with AI explanation without DB write
            group.MapPost("/preview", async (
                [FromBody] QuickForecastPreviewRequestDto request,
                [FromQuery] Guid? workspaceId,
                ITenantContext tenantContext,
                [FromServices] IDispatcher dispatcher,
                CancellationToken ct) =>
            {
                Guid? targetWorkspaceId = workspaceId ?? tenantContext.WorkspaceId;
                if (!targetWorkspaceId.HasValue)
                {
                    return Results.BadRequest(new { error = "Workspace ID is required." });
                }

                var query = new QuickForecastPreviewQuery(targetWorkspaceId.Value, request);
                var result = await dispatcher.QueryAsync(query, ct);
                return result.ToHttpResult();
            })
            .WithName($"PreviewForecast_{group.GetHashCode()}")
            .WithSummary("Computes statistical projections and preview trajectory curves on the fly.");

            // 5. DELETE /api/forecasts/{id:guid} - Delete saved forecast
            group.MapDelete("/{id:guid}", async (
                [FromRoute] Guid id,
                [FromQuery] Guid? workspaceId,
                ITenantContext tenantContext,
                [FromServices] IDispatcher dispatcher,
                CancellationToken ct) =>
            {
                Guid? targetWorkspaceId = workspaceId ?? tenantContext.WorkspaceId;
                if (!targetWorkspaceId.HasValue)
                {
                    return Results.BadRequest(new { error = "Workspace ID is required." });
                }

                var command = new DeleteForecastCommand(targetWorkspaceId.Value, id);
                var result = await dispatcher.SendAsync(command, ct);
                return result.ToHttpResult();
            })
            .WithName($"DeleteForecast_{group.GetHashCode()}")
            .WithSummary("Deletes a saved forecast from the workspace.");
        }

        return app;
    }
}
