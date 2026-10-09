using System.Security.Claims;
using IntelliQueryAI.Api.Extensions;
using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Application.Features.Ai.Commands.AnalyzeData;
using IntelliQueryAI.Application.Features.Ai.Commands.ExecuteChatQuery;
using IntelliQueryAI.Application.Features.Ai.Dtos;
using IntelliQueryAI.Application.Features.Ai.Queries;
using IntelliQueryAI.Application.Features.Ai.Queries.ResolveAiContext;
using IntelliQueryAI.Domain.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

using System.Text.Json.Serialization;
using IntelliQueryAI.Application.Common.Serialization;

namespace IntelliQueryAI.Api.Endpoints;

public sealed record ExecuteChatQueryRequest(
    Guid DataSourceId,
    string Question,
    [property: JsonConverter(typeof(FlexibleNullableGuidConverter))]
    Guid? WorkspaceId = null,
    [property: JsonConverter(typeof(FlexibleNullableGuidConverter))]
    Guid? SessionId = null,
    AiContextDto? Context = null);

public sealed record AnalyzeDataRequest(
    string Question,
    string DataJson,
    [property: JsonConverter(typeof(FlexibleNullableGuidConverter))]
    Guid? WorkspaceId = null);

public sealed record ResolveAiContextRequest(
    string ContextType,
    [property: JsonConverter(typeof(FlexibleNullableGuidConverter))]
    Guid? WorkspaceId = null,
    [property: JsonConverter(typeof(FlexibleNullableGuidConverter))]
    Guid? EntityId = null,
    [property: JsonConverter(typeof(FlexibleNullableGuidConverter))]
    Guid? DataSourceId = null,
    string? Metric = null,
    string? DateRange = null,
    string? EntityTitle = null,
    [property: JsonConverter(typeof(FlexibleStringDictionaryConverter))]
    IReadOnlyDictionary<string, string>? AdditionalMetadata = null);

public static class ChatEndpoints
{
    public static IEndpointRouteBuilder MapChatEndpoints(this IEndpointRouteBuilder app)
    {
        var groups = new[]
        {
            app.MapGroup("/api/chat").WithTags("AI Chat & Analytics"),
            app.MapGroup("/chat").WithTags("AI Chat & Analytics"),
            app.MapGroup("/api/ai").WithTags("AI Chat & Analytics")
        };

        foreach (var group in groups)
        {
            // 1. Full Multi-Agent Query Pipeline (POST /chat/query)
            group.MapPost("/query", async (
                [FromBody] ExecuteChatQueryRequest request,
                ClaimsPrincipal user,
                ITenantContext tenantContext,
                [FromServices] IDispatcher dispatcher,
                CancellationToken ct) =>
            {
                Guid? workspaceId = request.WorkspaceId ?? tenantContext.WorkspaceId;
                if (!workspaceId.HasValue)
                {
                    return Results.BadRequest(new { error = "Workspace ID is required." });
                }

                string? sub = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
                Guid userId = Guid.TryParse(sub, out var parsedId) ? parsedId : Guid.Empty;
                string userRole = user.FindFirstValue(ClaimTypes.Role) ?? user.FindFirstValue("role") ?? "Member";

                var command = new ExecuteChatQueryCommand(
                    workspaceId.Value,
                    request.DataSourceId,
                    userId,
                    request.Question,
                    request.SessionId,
                    request.Context,
                    userRole);

                var result = await dispatcher.SendAsync(command, ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization()
            .WithName($"ExecuteChatQuery_{group.GetHashCode()}")
            .WithSummary("Executes end-to-end multi-agent pipeline: Question -> Intent -> Vector Retrieval -> SQL Gen -> Validation -> Execution -> Insights -> Visualization.");

            // 2. Direct Data Analysis & Chart Recommendation (POST /chat/analyze)
            group.MapPost("/analyze", async (
                [FromBody] AnalyzeDataRequest request,
                ClaimsPrincipal user,
                ITenantContext tenantContext,
                [FromServices] IDispatcher dispatcher,
                CancellationToken ct) =>
            {
                Guid? workspaceId = request.WorkspaceId ?? tenantContext.WorkspaceId;
                if (!workspaceId.HasValue)
                {
                    return Results.BadRequest(new { error = "Workspace ID is required." });
                }

                string? sub = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
                Guid userId = Guid.TryParse(sub, out var parsedId) ? parsedId : Guid.Empty;

                var command = new AnalyzeDataCommand(
                    workspaceId.Value,
                    userId,
                    request.Question,
                    request.DataJson);

                var result = await dispatcher.SendAsync(command, ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization()
            .WithName($"AnalyzeData_{group.GetHashCode()}")
            .WithSummary("Analyzes provided tabular data to produce statistical insights and chart recommendations.");

            // 3. Contextual AI Resolution (POST /context/resolve)
            group.MapPost("/context/resolve", async (
                [FromBody] ResolveAiContextRequest request,
                ClaimsPrincipal user,
                ITenantContext tenantContext,
                [FromServices] IDispatcher dispatcher,
                CancellationToken ct) =>
            {
                Guid? workspaceId = request.WorkspaceId ?? tenantContext.WorkspaceId;
                if (!workspaceId.HasValue)
                {
                    return Results.BadRequest(new { error = "Workspace ID is required." });
                }

                var query = new ResolveAiContextQuery(
                    workspaceId.Value,
                    request.ContextType,
                    request.EntityId,
                    request.DataSourceId,
                    request.Metric,
                    request.DateRange,
                    request.EntityTitle,
                    request.AdditionalMetadata);

                var result = await dispatcher.QueryAsync(query, ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization()
            .WithName($"ResolveAiContext_{group.GetHashCode()}")
            .WithSummary("Validates and resolves application context (KPI, Alert, Anomaly, Report, etc.) for contextual AI queries.");
        }

        // Map GET /query-history and /api/query-history
        var historyGroups = new[]
        {
            app.MapGroup("/api/query-history").WithTags("Query History"),
            app.MapGroup("/query-history").WithTags("Query History")
        };

        foreach (var hGroup in historyGroups)
        {
            hGroup.MapGet("/", async (
                [FromQuery] Guid? workspaceId,
                [FromQuery] int? page,
                [FromQuery] int? pageSize,
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

                if (page.HasValue || pageSize.HasValue)
                {
                    int effectivePage = page.GetValueOrDefault(1);
                    int effectivePageSize = pageSize.GetValueOrDefault(10);
                    var pagedQuery = new GetPagedQueryHistoryQuery(targetWorkspaceId.Value, effectivePage, effectivePageSize);
                    var pagedResult = await dispatcher.QueryAsync(pagedQuery, ct);
                    return pagedResult.ToHttpResult();
                }

                var query = new GetQueryHistoryQuery(targetWorkspaceId.Value, limit.HasValue && limit.Value > 0 ? limit.Value : 50);
                var result = await dispatcher.QueryAsync(query, ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization()
            .WithName($"GetQueryHistory_{hGroup.GetHashCode()}")
            .WithSummary("Retrieves analytical query execution history with pagination support (page, pageSize) or legacy limit.");
        }

        return app;
    }
}
