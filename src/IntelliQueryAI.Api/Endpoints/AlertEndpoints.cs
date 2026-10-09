using System.Security.Claims;
using IntelliQueryAI.Api.Extensions;
using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Application.Features.Alerts.Commands.AcknowledgeAlert;
using IntelliQueryAI.Application.Features.Alerts.Dtos;
using IntelliQueryAI.Application.Features.Alerts.Queries.GetAlerts;
using IntelliQueryAI.Application.Features.Alerts.Queries.GetOpenAlerts;
using IntelliQueryAI.Domain.Enums;
using IntelliQueryAI.Domain.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace IntelliQueryAI.Api.Endpoints;

public static class AlertEndpoints
{
    public static IEndpointRouteBuilder MapAlertEndpoints(this IEndpointRouteBuilder app)
    {
        var groups = new[]
        {
            app.MapGroup("/api/alerts").WithTags("Intelligent Alerts"),
            app.MapGroup("/alerts").WithTags("Intelligent Alerts")
        };

        foreach (var group in groups)
        {
            // 1. GET /alerts - List alerts
            group.MapGet("/", async (
                [FromQuery] Guid? workspaceId,
                [FromQuery] bool? onlyOpen,
                [FromQuery] AlertSeverity? severity,
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

                int effectiveLimit = limit > 0 ? limit : 100;
                var query = new GetAlertsQuery(targetWorkspaceId.Value, onlyOpen, severity, effectiveLimit);
                var result = await dispatcher.QueryAsync(query, ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization()
            .WithName($"GetAlerts_{group.GetHashCode()}")
            .WithSummary("Retrieves alerts for the workspace with optional filtering by open status and severity.");

            // 2. GET /alerts/open - List open alerts with severity breakdown
            group.MapGet("/open", async (
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

                var query = new GetOpenAlertsQuery(targetWorkspaceId.Value);
                var result = await dispatcher.QueryAsync(query, ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization()
            .WithName($"GetOpenAlerts_{group.GetHashCode()}")
            .WithSummary("Retrieves all unacknowledged open alerts with severity count breakdown.");

            // 3. POST /alerts/acknowledge - Acknowledge alert
            group.MapPost("/acknowledge", async (
                [FromBody] AcknowledgeAlertRequest request,
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

                var command = new AcknowledgeAlertCommand(targetWorkspaceId.Value, request.AlertId, userId);
                var result = await dispatcher.SendAsync(command, ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization()
            .WithName($"AcknowledgeAlert_{group.GetHashCode()}")
            .WithSummary("Marks an alert as acknowledged by the current authenticated user.");
        }

        return app;
    }
}
