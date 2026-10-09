using System.Security.Claims;
using IntelliQueryAI.Api.Extensions;
using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Application.Features.Dashboards.Commands;
using IntelliQueryAI.Application.Features.Dashboards.Dtos;
using IntelliQueryAI.Application.Features.Dashboards.Queries;
using IntelliQueryAI.Domain.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace IntelliQueryAI.Api.Endpoints;

public static class CustomDashboardEndpoints
{
    public static IEndpointRouteBuilder MapCustomDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        var groups = new[]
        {
            app.MapGroup("/api/dashboards").WithTags("Custom Dashboards & AI Generator"),
            app.MapGroup("/dashboards").WithTags("Custom Dashboards & AI Generator")
        };

        foreach (var group in groups)
        {
            // 1. POST /dashboards/generate - Generate structured dashboard definition via AI
            group.MapPost("/generate", async (
                [FromBody] GenerateDashboardFromPromptRequestDto request,
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

                var command = new GenerateDashboardWithAiCommand(
                    targetWorkspaceId.Value,
                    userId,
                    request.Prompt,
                    request.DataSourceId,
                    request.DateRange);

                var result = await dispatcher.SendAsync(command, ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization()
            .WithName($"GenerateCustomDashboard_{group.GetHashCode()}")
            .WithSummary("Generates a safe structured dashboard definition using Microsoft Agent Framework.");

            // 2. GET /dashboards - List all dashboards for the workspace
            group.MapGet("/", async (
                [FromQuery] Guid? workspaceId,
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

                var query = new GetCustomDashboardsQuery(targetWorkspaceId.Value, limit ?? 50);
                var result = await dispatcher.QueryAsync(query, ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization()
            .WithName($"GetCustomDashboards_{group.GetHashCode()}")
            .WithSummary("Retrieves all saved custom dashboards for the active workspace.");

            // 3. POST /dashboards - Save new custom dashboard
            group.MapPost("/", async (
                [FromBody] CreateCustomDashboardRequestDto request,
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

                var command = new CreateCustomDashboardCommand(
                    targetWorkspaceId.Value,
                    userId,
                    request.Title,
                    request.Description,
                    request.Prompt,
                    request.DateRange,
                    request.Widgets,
                    request.IsDefault,
                    request.IsShared);

                var result = await dispatcher.SendAsync(command, ct);
                return result.ToHttpResult(StatusCodes.Status201Created);
            })
            .RequireAuthorization()
            .WithName($"CreateCustomDashboard_{group.GetHashCode()}")
            .WithSummary("Saves a custom dashboard with validated widget configurations.");

            // 4. GET /dashboards/{id:guid} - Get dashboard by ID (with optional widget data hydration)
            group.MapGet("/{id:guid}", async (
                Guid id,
                [FromQuery] Guid? workspaceId,
                [FromQuery] bool? hydrateData,
                [FromQuery] Guid? dataSourceId,
                ITenantContext tenantContext,
                [FromServices] IDispatcher dispatcher,
                CancellationToken ct) =>
            {
                Guid? targetWorkspaceId = workspaceId ?? tenantContext.WorkspaceId;
                if (!targetWorkspaceId.HasValue)
                {
                    return Results.BadRequest(new { error = "Workspace ID is required." });
                }

                var query = new GetCustomDashboardByIdQuery(
                    targetWorkspaceId.Value,
                    id,
                    HydrateData: hydrateData ?? false,
                    DataSourceId: dataSourceId);

                var result = await dispatcher.QueryAsync(query, ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization()
            .WithName($"GetCustomDashboardById_{group.GetHashCode()}")
            .WithSummary("Retrieves a custom dashboard by ID, optionally hydrating live widget data.");

            // 5. PUT /dashboards/{id:guid} - Update custom dashboard
            group.MapPut("/{id:guid}", async (
                Guid id,
                [FromBody] UpdateCustomDashboardRequestDto request,
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

                var command = new UpdateCustomDashboardCommand(
                    targetWorkspaceId.Value,
                    id,
                    request.Title,
                    request.Description,
                    request.DateRange,
                    request.Widgets,
                    request.IsDefault,
                    request.IsShared);

                var result = await dispatcher.SendAsync(command, ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization()
            .WithName($"UpdateCustomDashboard_{group.GetHashCode()}")
            .WithSummary("Updates title, description, layout, date range, or sharing for a custom dashboard.");

            // 6. POST /dashboards/{id:guid}/duplicate - Duplicate custom dashboard
            group.MapPost("/{id:guid}/duplicate", async (
                Guid id,
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

                var command = new DuplicateCustomDashboardCommand(
                    targetWorkspaceId.Value,
                    id,
                    userId);

                var result = await dispatcher.SendAsync(command, ct);
                return result.ToHttpResult(StatusCodes.Status201Created);
            })
            .RequireAuthorization()
            .WithName($"DuplicateCustomDashboard_{group.GetHashCode()}")
            .WithSummary("Duplicates an existing custom dashboard.");

            // 7. DELETE /dashboards/{id:guid} - Delete custom dashboard
            group.MapDelete("/{id:guid}", async (
                Guid id,
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

                var command = new DeleteCustomDashboardCommand(targetWorkspaceId.Value, id);
                var result = await dispatcher.SendAsync(command, ct);
                return result.ToHttpResult(StatusCodes.Status204NoContent);
            })
            .RequireAuthorization()
            .WithName($"DeleteCustomDashboard_{group.GetHashCode()}")
            .WithSummary("Deletes a custom dashboard.");

            // 8. POST /dashboards/{id:guid}/widgets/{widgetId}/query - Refresh single widget query
            group.MapPost("/{id:guid}/widgets/{widgetId}/query", async (
                Guid id,
                string widgetId,
                [FromQuery] Guid? workspaceId,
                [FromQuery] Guid? dataSourceId,
                ITenantContext tenantContext,
                [FromServices] IDispatcher dispatcher,
                CancellationToken ct) =>
            {
                Guid? targetWorkspaceId = workspaceId ?? tenantContext.WorkspaceId;
                if (!targetWorkspaceId.HasValue)
                {
                    return Results.BadRequest(new { error = "Workspace ID is required." });
                }

                var query = new ExecuteDashboardWidgetQuery(
                    targetWorkspaceId.Value,
                    id,
                    widgetId,
                    dataSourceId);

                var result = await dispatcher.QueryAsync(query, ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization()
            .WithName($"ExecuteDashboardWidgetQuery_{group.GetHashCode()}")
            .WithSummary("Refreshes the live data query for a specific dashboard widget.");
        }

        return app;
    }
}
