using System.Security.Claims;
using IntelliQueryAI.Api.Extensions;
using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Application.Features.Scenarios.Commands;
using IntelliQueryAI.Application.Features.Scenarios.Dtos;
using IntelliQueryAI.Application.Features.Scenarios.Queries;
using IntelliQueryAI.Domain.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace IntelliQueryAI.Api.Endpoints;

public static class ScenarioEndpoints
{
    public static IEndpointRouteBuilder MapScenarioEndpoints(this IEndpointRouteBuilder app)
    {
        var groups = new[]
        {
            app.MapGroup("/api/scenarios").WithTags("What-If & Scenario Analysis").RequireAuthorization(),
            app.MapGroup("/scenarios").WithTags("What-If & Scenario Analysis").RequireAuthorization()
        };

        foreach (var group in groups)
        {
            // 1. GET /api/scenarios - List scenarios for active workspace
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

                var query = new GetScenariosQuery(targetWorkspaceId.Value, limit ?? 50);
                var result = await dispatcher.QueryAsync(query, ct);
                return result.ToHttpResult();
            })
            .WithName($"GetScenarios_{group.GetHashCode()}")
            .WithSummary("Retrieves What-If scenarios for the active workspace.");

            // 2. POST /api/scenarios - Create a new scenario and compute initial projections
            group.MapPost("/", async (
                [FromBody] CreateScenarioRequestDto request,
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

                var command = new CreateScenarioCommand(
                    WorkspaceId: targetWorkspaceId.Value,
                    UserId: userId,
                    Title: request.Title,
                    Description: request.Description,
                    TargetMetric: request.TargetMetric,
                    BaselineValue: request.BaselineValue,
                    TimeHorizon: request.TimeHorizon,
                    Variables: request.Variables);

                var result = await dispatcher.SendAsync(command, ct);
                return result.ToHttpResult();
            })
            .WithName($"CreateScenario_{group.GetHashCode()}")
            .WithSummary("Creates a What-If scenario with deterministic statistical projections and AI narrative.");

            // 3. GET /api/scenarios/{id} - Get scenario details
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

                var query = new GetScenarioByIdQuery(targetWorkspaceId.Value, id);
                var result = await dispatcher.QueryAsync(query, ct);
                return result.ToHttpResult();
            })
            .WithName($"GetScenarioById_{group.GetHashCode()}")
            .WithSummary("Retrieves a What-If scenario by its unique identifier.");

            // 4. PUT /api/scenarios/{id} - Update scenario title & description
            group.MapPut("/{id:guid}", async (
                [FromRoute] Guid id,
                [FromBody] UpdateScenarioRequestDto request,
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

                var command = new UpdateScenarioCommand(targetWorkspaceId.Value, userId, id, request.Title, request.Description);
                var result = await dispatcher.SendAsync(command, ct);
                return result.ToHttpResult();
            })
            .WithName($"UpdateScenario_{group.GetHashCode()}")
            .WithSummary("Updates title and description for an existing scenario.");

            // 5. DELETE /api/scenarios/{id} - Delete scenario
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

                var command = new DeleteScenarioCommand(targetWorkspaceId.Value, userId, id);
                var result = await dispatcher.SendAsync(command, ct);
                return result.ToHttpResult();
            })
            .WithName($"DeleteScenario_{group.GetHashCode()}")
            .WithSummary("Deletes a What-If scenario.");

            // 6. POST /api/scenarios/{id}/rerun - Recalculate simulation with modified variable assumptions
            group.MapPost("/{id:guid}/rerun", async (
                [FromRoute] Guid id,
                [FromBody] RerunScenarioRequestDto? request,
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

                var command = new RerunScenarioSimulationCommand(
                    WorkspaceId: targetWorkspaceId.Value,
                    UserId: userId,
                    ScenarioId: id,
                    Variables: request?.Variables,
                    TimeHorizon: request?.TimeHorizon);

                var result = await dispatcher.SendAsync(command, ct);
                return result.ToHttpResult();
            })
            .WithName($"RerunScenario_{group.GetHashCode()}")
            .WithSummary("Recalculates What-If projections and updates AI executive strategic narrative.");

            // 7. POST /api/scenarios/simulate - Quick simulate sandbox
            group.MapPost("/simulate", async (
                [FromBody] QuickSimulateRequestDto request,
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

                var query = new QuickSimulateScenarioQuery(
                    WorkspaceId: targetWorkspaceId.Value,
                    TargetMetric: request.TargetMetric,
                    BaselineValue: request.BaselineValue,
                    TimeHorizon: request.TimeHorizon,
                    Variables: request.Variables);

                var result = await dispatcher.QueryAsync(query, ct);
                return result.ToHttpResult();
            })
            .WithName($"QuickSimulateScenario_{group.GetHashCode()}")
            .WithSummary("Interactive sandbox simulation without persisting to database.");
        }

        return app;
    }
}
