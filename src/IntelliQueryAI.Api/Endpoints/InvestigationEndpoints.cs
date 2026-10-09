using System.Security.Claims;
using IntelliQueryAI.Api.Extensions;
using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Application.Features.Investigations.Commands;
using IntelliQueryAI.Application.Features.Investigations.Queries;
using IntelliQueryAI.Domain.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace IntelliQueryAI.Api.Endpoints;

public sealed record StartInvestigationRequest(
    string Question,
    Guid? DataSourceId = null,
    string? TargetMetric = null);

public static class InvestigationEndpoints
{
    public static IEndpointRouteBuilder MapInvestigationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/investigations")
            .WithTags("Root-Cause Investigations")
            .RequireAuthorization();

        // 1. POST /api/investigations
        group.MapPost("/", async (
            [FromBody] StartInvestigationRequest request,
            [FromQuery] Guid? workspaceId,
            ITenantContext tenantContext,
            [FromServices] IDispatcher dispatcher,
            CancellationToken ct) =>
        {
            var targetWorkspaceId = workspaceId ?? tenantContext.WorkspaceId;
            if (!targetWorkspaceId.HasValue || targetWorkspaceId.Value == Guid.Empty)
            {
                return Results.BadRequest(new { error = "Workspace ID is required." });
            }

            var command = new StartInvestigationCommand(
                Question: request.Question,
                DataSourceId: request.DataSourceId,
                TargetMetric: request.TargetMetric,
                WorkspaceId: targetWorkspaceId.Value);

            var result = await dispatcher.SendAsync(command, ct);
            return result.ToHttpResult(StatusCodes.Status201Created);
        })
        .WithName("StartInvestigation")
        .WithSummary("Initiates an autonomous AI multi-step root-cause investigation.");

        // 2. GET /api/investigations
        group.MapGet("/", async (
            [FromQuery] Guid? workspaceId,
            [FromQuery] int? limit,
            ITenantContext tenantContext,
            [FromServices] IDispatcher dispatcher,
            CancellationToken ct) =>
        {
            var targetWorkspaceId = workspaceId ?? tenantContext.WorkspaceId;
            if (!targetWorkspaceId.HasValue || targetWorkspaceId.Value == Guid.Empty)
            {
                return Results.BadRequest(new { error = "Workspace ID is required." });
            }

            var query = new GetInvestigationsQuery(
                WorkspaceId: targetWorkspaceId.Value,
                Limit: limit ?? 50);

            var result = await dispatcher.QueryAsync(query, ct);
            return result.ToHttpResult();
        })
        .WithName("GetInvestigations")
        .WithSummary("Retrieves all historical investigations for the workspace.");

        // 3. GET /api/investigations/{id}
        group.MapGet("/{id:guid}", async (
            [FromRoute] Guid id,
            [FromQuery] Guid? workspaceId,
            ITenantContext tenantContext,
            [FromServices] IDispatcher dispatcher,
            CancellationToken ct) =>
        {
            var targetWorkspaceId = workspaceId ?? tenantContext.WorkspaceId;
            var query = new GetInvestigationByIdQuery(
                Id: id,
                WorkspaceId: targetWorkspaceId);

            var result = await dispatcher.QueryAsync(query, ct);
            return result.ToHttpResult();
        })
        .WithName("GetInvestigationById")
        .WithSummary("Retrieves detailed investigation findings, evidence, charts, and recommendations by ID.");

        // 4. DELETE /api/investigations/{id}
        group.MapDelete("/{id:guid}", async (
            [FromRoute] Guid id,
            [FromQuery] Guid? workspaceId,
            ITenantContext tenantContext,
            [FromServices] IDispatcher dispatcher,
            CancellationToken ct) =>
        {
            var targetWorkspaceId = workspaceId ?? tenantContext.WorkspaceId;
            var command = new DeleteInvestigationCommand(
                Id: id,
                WorkspaceId: targetWorkspaceId);

            var result = await dispatcher.SendAsync(command, ct);
            return result.ToHttpResult();
        })
        .WithName("DeleteInvestigation")
        .WithSummary("Deletes an investigation report.");

        return app;
    }
}
