using System.Security.Claims;
using IntelliQueryAI.Api.Extensions;
using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Application.Features.Ai.Commands.SaveReport;
using IntelliQueryAI.Application.Features.Ai.Queries;
using IntelliQueryAI.Domain.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace IntelliQueryAI.Api.Endpoints;

public sealed record SaveReportRequest(
    string Title,
    string? Description,
    string Question,
    string? SqlQuery = null,
    string? GeneratedSql = null,
    string? ChartConfigurationJson = null,
    string? ChartType = null,
    string? InsightsJson = null,
    Guid? DataSourceId = null,
    Guid? WorkspaceId = null);

public static class ReportEndpoints
{
    public static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder app)
    {
        var groups = new[]
        {
            app.MapGroup("/api/reports").WithTags("Saved Reports & Dashboards"),
            app.MapGroup("/reports").WithTags("Saved Reports & Dashboards")
        };

        foreach (var group in groups)
        {
            // 1. List saved reports for workspace (GET /reports)
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

                var query = new GetSavedReportsQuery(targetWorkspaceId.Value);
                var result = await dispatcher.QueryAsync(query, ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization()
            .WithName($"GetSavedReports_{group.GetHashCode()}")
            .WithSummary("Retrieves all saved reports, dashboard charts, and queries for the workspace.");

            // 2. Save a report (POST /reports/save)
            group.MapPost("/save", async (
                [FromBody] SaveReportRequest request,
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

                string effectiveSql = !string.IsNullOrWhiteSpace(request.SqlQuery)
                    ? request.SqlQuery
                    : (!string.IsNullOrWhiteSpace(request.GeneratedSql) ? request.GeneratedSql : string.Empty);

                string chartConfig = !string.IsNullOrWhiteSpace(request.ChartConfigurationJson)
                    ? request.ChartConfigurationJson
                    : (!string.IsNullOrWhiteSpace(request.ChartType)
                        ? $"{{\"chartType\":\"{request.ChartType}\"}}"
                        : "{}");

                string insights = !string.IsNullOrWhiteSpace(request.InsightsJson)
                    ? request.InsightsJson
                    : "[]";

                var command = new SaveReportCommand(
                    workspaceId.Value,
                    userId,
                    request.Title,
                    request.Description,
                    request.Question,
                    effectiveSql,
                    chartConfig,
                    insights);

                var result = await dispatcher.SendAsync(command, ct);
                return result.ToHttpResult(StatusCodes.Status201Created);
            })
            .RequireAuthorization()
            .WithName($"SaveReport_{group.GetHashCode()}")
            .WithSummary("Saves an analytical query, chart layout, and business insights as a persistent report.");
        }

        return app;
    }
}
