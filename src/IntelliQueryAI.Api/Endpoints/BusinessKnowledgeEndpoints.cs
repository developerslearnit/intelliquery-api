using System.Security.Claims;
using IntelliQueryAI.Api.Extensions;
using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Application.Features.BusinessKnowledge.Commands;
using IntelliQueryAI.Application.Features.BusinessKnowledge.Queries;
using IntelliQueryAI.Domain.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace IntelliQueryAI.Api.Endpoints;

public sealed record CreateBusinessTermRequest(
    string Name,
    string Definition,
    IReadOnlyList<string>? Aliases = null,
    string Category = "General",
    string Classification = "Internal",
    string? OwnerName = null,
    string? OwnerEmail = null,
    string? TargetTable = null,
    string? TargetColumn = null,
    string? SqlCalculationRule = null);

public sealed record UpdateBusinessTermRequest(
    string Name,
    string Definition,
    IReadOnlyList<string>? Aliases = null,
    string Category = "General",
    string Classification = "Internal",
    string? OwnerName = null,
    string? OwnerEmail = null,
    string? TargetTable = null,
    string? TargetColumn = null,
    string? SqlCalculationRule = null);

public sealed record ToggleStatusRequest(bool IsActive);

public sealed record CreateMetricRequest(
    string Name,
    string Description,
    string CalculationFormula,
    string Unit = "",
    Guid? DataSourceId = null,
    string? FilterExpression = null,
    string? TargetTable = null,
    string? TargetColumn = null,
    string AggregationType = "SUM",
    string TrendDirection = "HigherIsBetter");

public sealed record UpdateMetricRequest(
    string Name,
    string Description,
    string CalculationFormula,
    string Unit,
    Guid? DataSourceId,
    string? FilterExpression,
    string? TargetTable,
    string? TargetColumn,
    string AggregationType,
    string TrendDirection);

public sealed record ResolveSemanticContextRequest(string Prompt);

public static class BusinessKnowledgeEndpoints
{
    public static IEndpointRouteBuilder MapBusinessKnowledgeEndpoints(this IEndpointRouteBuilder app)
    {
        var groups = new[]
        {
            app.MapGroup("/api/knowledge").WithTags("Business Knowledge & Semantic Layer"),
            app.MapGroup("/knowledge").WithTags("Business Knowledge & Semantic Layer")
        };

        foreach (var group in groups)
        {
            // -------------------------------------------------------------
            // Business Terms Endpoints
            // -------------------------------------------------------------

            // 1. GET /terms
            group.MapGet("/terms", async (
                [FromQuery] Guid? workspaceId,
                [FromQuery] string? search,
                [FromQuery] string? category,
                [FromQuery] bool? isActive,
                ITenantContext tenantContext,
                [FromServices] IDispatcher dispatcher,
                CancellationToken ct) =>
            {
                Guid? targetWorkspaceId = workspaceId ?? tenantContext.WorkspaceId;
                if (!targetWorkspaceId.HasValue)
                {
                    return Results.BadRequest(new { error = "Workspace ID is required." });
                }

                var query = new GetBusinessTermsQuery(targetWorkspaceId.Value, search, category, isActive);
                var result = await dispatcher.QueryAsync(query, ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization()
            .WithName($"GetBusinessTerms_{group.GetHashCode()}")
            .WithSummary("Retrieves all defined business terms and glossary definitions for the workspace.");

            // 2. POST /terms
            group.MapPost("/terms", async (
                [FromBody] CreateBusinessTermRequest request,
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

                var command = new CreateBusinessTermCommand(
                    targetWorkspaceId.Value,
                    userId,
                    request.Name,
                    request.Definition,
                    request.Aliases,
                    request.Category,
                    request.Classification,
                    request.OwnerName,
                    request.OwnerEmail,
                    request.TargetTable,
                    request.TargetColumn,
                    request.SqlCalculationRule);

                var result = await dispatcher.SendAsync(command, ct);
                return result.ToHttpResult(StatusCodes.Status201Created);
            })
            .RequireAuthorization()
            .WithName($"CreateBusinessTerm_{group.GetHashCode()}")
            .WithSummary("Creates a new standardized business definition in the knowledge layer.");

            // 3. GET /terms/{id:guid}
            group.MapGet("/terms/{id:guid}", async (
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

                var query = new GetBusinessTermByIdQuery(targetWorkspaceId.Value, id);
                var result = await dispatcher.QueryAsync(query, ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization()
            .WithName($"GetBusinessTermById_{group.GetHashCode()}")
            .WithSummary("Retrieves a business term by ID.");

            // 4. PUT /terms/{id:guid}
            group.MapPut("/terms/{id:guid}", async (
                [FromRoute] Guid id,
                [FromBody] UpdateBusinessTermRequest request,
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

                var command = new UpdateBusinessTermCommand(
                    targetWorkspaceId.Value,
                    userId,
                    id,
                    request.Name,
                    request.Definition,
                    request.Aliases,
                    request.Category,
                    request.Classification,
                    request.OwnerName,
                    request.OwnerEmail,
                    request.TargetTable,
                    request.TargetColumn,
                    request.SqlCalculationRule);

                var result = await dispatcher.SendAsync(command, ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization()
            .WithName($"UpdateBusinessTerm_{group.GetHashCode()}")
            .WithSummary("Updates an existing business term.");

            // 5. DELETE /terms/{id:guid}
            group.MapDelete("/terms/{id:guid}", async (
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

                var command = new DeleteBusinessTermCommand(targetWorkspaceId.Value, userId, id);
                var result = await dispatcher.SendAsync(command, ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization()
            .WithName($"DeleteBusinessTerm_{group.GetHashCode()}")
            .WithSummary("Deletes a business term.");

            // 6. PATCH /terms/{id:guid}/toggle-status
            group.MapPatch("/terms/{id:guid}/toggle-status", async (
                [FromRoute] Guid id,
                [FromBody] ToggleStatusRequest request,
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

                var command = new ToggleBusinessTermStatusCommand(targetWorkspaceId.Value, userId, id, request.IsActive);
                var result = await dispatcher.SendAsync(command, ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization()
            .WithName($"ToggleBusinessTermStatus_{group.GetHashCode()}")
            .WithSummary("Enables or disables a business term.");

            // -------------------------------------------------------------
            // Metric Definitions Endpoints
            // -------------------------------------------------------------

            // 7. GET /metrics
            group.MapGet("/metrics", async (
                [FromQuery] Guid? workspaceId,
                [FromQuery] string? search,
                [FromQuery] bool? isActive,
                ITenantContext tenantContext,
                [FromServices] IDispatcher dispatcher,
                CancellationToken ct) =>
            {
                Guid? targetWorkspaceId = workspaceId ?? tenantContext.WorkspaceId;
                if (!targetWorkspaceId.HasValue)
                {
                    return Results.BadRequest(new { error = "Workspace ID is required." });
                }

                var query = new GetMetricDefinitionsQuery(targetWorkspaceId.Value, search, isActive);
                var result = await dispatcher.QueryAsync(query, ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization()
            .WithName($"GetMetricDefinitions_{group.GetHashCode()}")
            .WithSummary("Retrieves all defined operational metrics and KPIs for the workspace.");

            // 8. POST /metrics
            group.MapPost("/metrics", async (
                [FromBody] CreateMetricRequest request,
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

                var command = new CreateMetricDefinitionCommand(
                    targetWorkspaceId.Value,
                    userId,
                    request.Name,
                    request.Description,
                    request.CalculationFormula,
                    request.Unit,
                    request.DataSourceId,
                    request.FilterExpression,
                    request.TargetTable,
                    request.TargetColumn,
                    request.AggregationType,
                    request.TrendDirection);

                var result = await dispatcher.SendAsync(command, ct);
                return result.ToHttpResult(StatusCodes.Status201Created);
            })
            .RequireAuthorization()
            .WithName($"CreateMetricDefinition_{group.GetHashCode()}")
            .WithSummary("Creates a new metric or KPI calculation rule.");

            // 9. GET /metrics/{id:guid}
            group.MapGet("/metrics/{id:guid}", async (
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

                var query = new GetMetricDefinitionByIdQuery(targetWorkspaceId.Value, id);
                var result = await dispatcher.QueryAsync(query, ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization()
            .WithName($"GetMetricDefinitionById_{group.GetHashCode()}")
            .WithSummary("Retrieves a metric definition by ID.");

            // 10. PUT /metrics/{id:guid}
            group.MapPut("/metrics/{id:guid}", async (
                [FromRoute] Guid id,
                [FromBody] UpdateMetricRequest request,
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

                var command = new UpdateMetricDefinitionCommand(
                    targetWorkspaceId.Value,
                    userId,
                    id,
                    request.Name,
                    request.Description,
                    request.CalculationFormula,
                    request.Unit,
                    request.DataSourceId,
                    request.FilterExpression,
                    request.TargetTable,
                    request.TargetColumn,
                    request.AggregationType,
                    request.TrendDirection);

                var result = await dispatcher.SendAsync(command, ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization()
            .WithName($"UpdateMetricDefinition_{group.GetHashCode()}")
            .WithSummary("Updates an existing metric definition.");

            // 11. DELETE /metrics/{id:guid}
            group.MapDelete("/metrics/{id:guid}", async (
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

                var command = new DeleteMetricDefinitionCommand(targetWorkspaceId.Value, userId, id);
                var result = await dispatcher.SendAsync(command, ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization()
            .WithName($"DeleteMetricDefinition_{group.GetHashCode()}")
            .WithSummary("Deletes a metric definition.");

            // 12. POST /resolve (Semantic Context Resolution for AI)
            group.MapPost("/resolve", async (
                [FromBody] ResolveSemanticContextRequest request,
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

                var query = new ResolveSemanticContextQuery(targetWorkspaceId.Value, request.Prompt);
                var result = await dispatcher.QueryAsync(query, ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization()
            .WithName($"ResolveSemanticContext_{group.GetHashCode()}")
            .WithSummary("Extracts and resolves relevant business terms and metric definitions for a given user prompt.");
        }

        return app;
    }
}
