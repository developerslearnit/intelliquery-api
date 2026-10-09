using System.Security.Claims;
using IntelliQueryAI.Api.Extensions;
using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Application.Features.Recommendations.Commands;
using IntelliQueryAI.Application.Features.Recommendations.Dtos;
using IntelliQueryAI.Application.Features.Recommendations.Queries;
using IntelliQueryAI.Domain.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace IntelliQueryAI.Api.Endpoints;

public static class RecommendationEndpoints
{
    public static IEndpointRouteBuilder MapRecommendationEndpoints(this IEndpointRouteBuilder app)
    {
        var groups = new[]
        {
            app.MapGroup("/api/recommendations").WithTags("Actionable Recommendations").RequireAuthorization(),
            app.MapGroup("/recommendations").WithTags("Actionable Recommendations").RequireAuthorization()
        };

        foreach (var group in groups)
        {
            // 1. GET /api/recommendations - List recommendations for active workspace
            group.MapGet("/", async (
                [FromQuery] Guid? workspaceId,
                [FromQuery] string? status,
                [FromQuery] string? priority,
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

                var query = new GetRecommendationsQuery(targetWorkspaceId.Value, status, priority, limit ?? 50);
                var result = await dispatcher.QueryAsync(query, ct);
                return result.ToHttpResult();
            })
            .WithName($"GetRecommendations_{group.GetHashCode()}")
            .WithSummary("Retrieves evidence-backed recommendations for the active workspace.");

            // 2. GET /api/recommendations/summary - KPI overview counters
            group.MapGet("/summary", async (
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

                var query = new GetRecommendationSummaryQuery(targetWorkspaceId.Value);
                var result = await dispatcher.QueryAsync(query, ct);
                return result.ToHttpResult();
            })
            .WithName($"GetRecommendationSummary_{group.GetHashCode()}")
            .WithSummary("Retrieves recommendation summary metrics and status counts.");

            // 3. POST /api/recommendations/generate - Trigger AI cross-cutting synthesis
            group.MapPost("/generate", async (
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

                var command = new GenerateRecommendationsCommand(targetWorkspaceId.Value, userId);
                var result = await dispatcher.SendAsync(command, ct);
                return result.ToHttpResult();
            })
            .WithName($"GenerateRecommendations_{group.GetHashCode()}")
            .WithSummary("Synthesizes evidence-backed recommendations from workspace signals using Microsoft Agent Framework.");

            // 4. POST /api/recommendations - Manual recommendation creation
            group.MapPost("/", async (
                [FromBody] CreateRecommendationRequestDto request,
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

                var command = new CreateRecommendationCommand(targetWorkspaceId.Value, userId, request);
                var result = await dispatcher.SendAsync(command, ct);
                return result.ToHttpResult();
            })
            .WithName($"CreateRecommendation_{group.GetHashCode()}")
            .WithSummary("Creates an evidence-backed recommendation.");

            // 5. GET /api/recommendations/{id} - Get recommendation details
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

                var query = new GetRecommendationByIdQuery(targetWorkspaceId.Value, id);
                var result = await dispatcher.QueryAsync(query, ct);
                return result.ToHttpResult();
            })
            .WithName($"GetRecommendationById_{group.GetHashCode()}")
            .WithSummary("Retrieves a recommendation by its unique identifier.");

            // 6. POST /api/recommendations/{id}/apply - Mark recommendation as applied
            group.MapPost("/{id:guid}/apply", async (
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

                var command = new ApplyRecommendationCommand(targetWorkspaceId.Value, userId, id);
                var result = await dispatcher.SendAsync(command, ct);
                return result.ToHttpResult();
            })
            .WithName($"ApplyRecommendation_{group.GetHashCode()}")
            .WithSummary("Marks a recommendation as applied / accepted by an authorized operator.");

            // 7. POST /api/recommendations/{id}/dismiss - Dismiss recommendation
            group.MapPost("/{id:guid}/dismiss", async (
                [FromRoute] Guid id,
                [FromBody] DismissRecommendationRequestDto? request,
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

                var command = new DismissRecommendationCommand(targetWorkspaceId.Value, userId, id, request?.Reason);
                var result = await dispatcher.SendAsync(command, ct);
                return result.ToHttpResult();
            })
            .WithName($"DismissRecommendation_{group.GetHashCode()}")
            .WithSummary("Dismisses a recommendation with optional justification reason.");

            // 8. POST /api/recommendations/{id}/reactivate - Reactivate dismissed/applied recommendation
            group.MapPost("/{id:guid}/reactivate", async (
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

                var command = new ReactivateRecommendationCommand(targetWorkspaceId.Value, userId, id);
                var result = await dispatcher.SendAsync(command, ct);
                return result.ToHttpResult();
            })
            .WithName($"ReactivateRecommendation_{group.GetHashCode()}")
            .WithSummary("Reactivates a dismissed or applied recommendation back to Active status.");

            // 9. DELETE /api/recommendations/{id} - Delete recommendation
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

                var command = new DeleteRecommendationCommand(targetWorkspaceId.Value, userId, id);
                var result = await dispatcher.SendAsync(command, ct);
                return result.ToHttpResult();
            })
            .WithName($"DeleteRecommendation_{group.GetHashCode()}")
            .WithSummary("Deletes a recommendation.");
        }

        return app;
    }
}
