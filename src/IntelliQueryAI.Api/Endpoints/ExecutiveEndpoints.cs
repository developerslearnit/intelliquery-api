using System.Security.Claims;
using IntelliQueryAI.Api.Extensions;
using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Application.Features.ExecutiveIntelligence.Commands.GenerateExecutiveReport;
using IntelliQueryAI.Application.Features.ExecutiveIntelligence.Dtos;
using IntelliQueryAI.Application.Features.ExecutiveIntelligence.Queries.GetExecutiveReports;
using IntelliQueryAI.Application.Features.ExecutiveIntelligence.Queries.GetLatestExecutiveReport;
using IntelliQueryAI.Domain.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace IntelliQueryAI.Api.Endpoints;

public static class ExecutiveEndpoints
{
    public static IEndpointRouteBuilder MapExecutiveEndpoints(this IEndpointRouteBuilder app)
    {
        var groups = new[]
        {
            app.MapGroup("/api/executive-reports").WithTags("Executive Reports & Intelligence"),
            app.MapGroup("/executive-reports").WithTags("Executive Reports & Intelligence")
        };

        foreach (var group in groups)
        {
            // 1. GET /executive-reports
            group.MapGet("/", async (
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

                int effectiveLimit = limit > 0 ? limit : 20;
                var query = new GetExecutiveReportsQuery(targetWorkspaceId.Value, effectiveLimit);
                var result = await dispatcher.QueryAsync(query, ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization()
            .WithName($"GetExecutiveReports_{group.GetHashCode()}")
            .WithSummary("Retrieves historical executive intelligence reports.");

            // 2. GET /executive-reports/latest
            group.MapGet("/latest", async (
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

                var query = new GetLatestExecutiveReportQuery(targetWorkspaceId.Value);
                var result = await dispatcher.QueryAsync(query, ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization()
            .WithName($"GetLatestExecutiveReport_{group.GetHashCode()}")
            .WithSummary("Retrieves the latest executive briefing for the workspace.");

            // 2b. GET /executive-reports/{id}
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

                var query = new IntelliQueryAI.Application.Features.ExecutiveIntelligence.Queries.GetExecutiveReportById.GetExecutiveReportByIdQuery(targetWorkspaceId.Value, id);
                var result = await dispatcher.QueryAsync(query, ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization()
            .WithName($"GetExecutiveReportById_{group.GetHashCode()}")
            .WithSummary("Retrieves an executive report by ID.");

            // 3. POST /executive-reports/generate
            group.MapPost("/generate", async (
                [FromBody] GenerateExecutiveReportRequestDto? request,
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

                var command = new GenerateExecutiveReportCommand(
                    targetWorkspaceId.Value,
                    userId,
                    request?.ReportPeriod,
                    request?.CustomTitle);

                var result = await dispatcher.SendAsync(command, ct);
                return result.ToHttpResult(StatusCodes.Status201Created);
            })
            .RequireAuthorization()
            .WithName($"GenerateExecutiveReport_{group.GetHashCode()}")
            .WithSummary("Synthesizes a new executive intelligence briefing using specialized AI agents.");
        }

        return app;
    }
}
