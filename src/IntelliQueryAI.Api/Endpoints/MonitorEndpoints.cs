using System.Security.Claims;
using IntelliQueryAI.Api.Extensions;
using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Application.Features.Monitoring.Commands.CreateMonitor;
using IntelliQueryAI.Application.Features.Monitoring.Commands.DeleteMonitor;
using IntelliQueryAI.Application.Features.Monitoring.Commands.RunMonitor;
using IntelliQueryAI.Application.Features.Monitoring.Dtos;
using IntelliQueryAI.Application.Features.Monitoring.Queries.GetMonitorById;
using IntelliQueryAI.Application.Features.Monitoring.Queries.GetMonitorExecutions;
using IntelliQueryAI.Application.Features.Monitoring.Queries.GetMonitors;
using IntelliQueryAI.Domain.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace IntelliQueryAI.Api.Endpoints;

public static class MonitorEndpoints
{
    public static IEndpointRouteBuilder MapMonitorEndpoints(this IEndpointRouteBuilder app)
    {
        var groups = new[]
        {
            app.MapGroup("/api/monitors").WithTags("AI Monitors"),
            app.MapGroup("/monitors").WithTags("AI Monitors")
        };

        foreach (var group in groups)
        {
            // 1. GET /monitors - List all monitors
            group.MapGet("/", async (
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

                var query = new GetMonitorsQuery(targetWorkspaceId.Value);
                var result = await dispatcher.QueryAsync(query, ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization()
            .WithName($"GetMonitors_{group.GetHashCode()}")
            .WithSummary("Retrieves all configured AI monitors for the workspace.");

            // 2. POST /monitors - Create monitor
            group.MapPost("/", async (
                [FromBody] CreateMonitorRequest request,
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

                string? sub = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
                Guid userId = Guid.TryParse(sub, out var parsedId) ? parsedId : Guid.Empty;

                var command = new CreateMonitorCommand(
                    targetWorkspaceId.Value,
                    userId,
                    request.DataSourceId,
                    request.Name,
                    request.Description,
                    request.MonitorType,
                    request.ScheduleType,
                    request.QueryTemplate,
                    request.TargetMetric,
                    request.Rules);

                var result = await dispatcher.SendAsync(command, ct);
                return result.ToHttpResult(StatusCodes.Status201Created);
            })
            .RequireAuthorization()
            .WithName($"CreateMonitor_{group.GetHashCode()}")
            .WithSummary("Creates a new AI Monitor with rules and automated schedule.");

            // 3. GET /monitors/{id} - Get monitor by ID
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

                var query = new GetMonitorByIdQuery(targetWorkspaceId.Value, id);
                var result = await dispatcher.QueryAsync(query, ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization()
            .WithName($"GetMonitorById_{group.GetHashCode()}")
            .WithSummary("Retrieves details, configured rules, and recent executions of a monitor.");

            // 4. POST /monitors/{id}/run - Trigger manual run
            group.MapPost("/{id:guid}/run", async (
                [FromRoute] Guid id,
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

                string? sub = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
                Guid userId = Guid.TryParse(sub, out var parsedId) ? parsedId : Guid.Empty;

                var command = new RunMonitorCommand(targetWorkspaceId.Value, id, userId);
                var result = await dispatcher.SendAsync(command, ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization()
            .WithName($"RunMonitor_{group.GetHashCode()}")
            .WithSummary("Triggers an on-demand immediate execution of the AI monitor.");

            // 5. DELETE /monitors/{id} - Delete monitor
            group.MapDelete("/{id:guid}", async (
                [FromRoute] Guid id,
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

                string? sub = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
                Guid userId = Guid.TryParse(sub, out var parsedId) ? parsedId : Guid.Empty;

                var command = new DeleteMonitorCommand(targetWorkspaceId.Value, id, userId);
                var result = await dispatcher.SendAsync(command, ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization()
            .WithName($"DeleteMonitor_{group.GetHashCode()}")
            .WithSummary("Deletes an AI monitor and its historical configurations.");

            // 6. GET /monitors/{id}/executions - Execution history
            group.MapGet("/{id:guid}/executions", async (
                [FromRoute] Guid id,
                [FromQuery] Guid? workspaceId,
                [FromQuery] int limit,
                ITenantContext tenantContext,
                [FromServices] IDispatcher dispatcher,
                CancellationToken ct) =>
            {
                Guid? targetWorkspaceId = workspaceId ?? tenantContext.WorkspaceId;
                if (!targetWorkspaceId.HasValue)
                {
                    return Results.BadRequest(new { error = "Workspace ID is required." });
                }

                int effectiveLimit = limit > 0 ? limit : 50;
                var query = new GetMonitorExecutionsQuery(targetWorkspaceId.Value, id, effectiveLimit);
                var result = await dispatcher.QueryAsync(query, ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization()
            .WithName($"GetMonitorExecutions_{group.GetHashCode()}")
            .WithSummary("Retrieves historical execution records and findings for a monitor.");
        }

        return app;
    }
}
