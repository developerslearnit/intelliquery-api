using System.Security.Claims;
using IntelliQueryAI.Api.Extensions;
using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Application.Features.Dashboard.Commands.GenerateMorningExecutiveBrief;
using IntelliQueryAI.Application.Features.Dashboard.Dtos;
using IntelliQueryAI.Application.Features.Dashboard.Queries.GetDashboardAlerts;
using IntelliQueryAI.Application.Features.Dashboard.Queries.GetDashboardAnomalies;
using IntelliQueryAI.Application.Features.Dashboard.Queries.GetDashboardExecutiveSummary;
using IntelliQueryAI.Application.Features.Dashboard.Queries.GetDashboardMonitoring;
using IntelliQueryAI.Application.Features.Dashboard.Queries.GetDashboardTrends;
using IntelliQueryAI.Application.Features.Dashboard.Queries.GetMorningExecutiveBrief;
using IntelliQueryAI.Application.Features.ExecutiveIntelligence.Commands.GenerateExecutiveReport;
using IntelliQueryAI.Application.Features.ExecutiveIntelligence.Dtos;
using IntelliQueryAI.Domain.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace IntelliQueryAI.Api.Endpoints;

public static class DashboardEndpoints
{
    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        var groups = new[]
        {
            app.MapGroup("/api/dashboard").WithTags("Executive Dashboard & Intelligence"),
            app.MapGroup("/dashboard").WithTags("Executive Dashboard & Intelligence")
        };

        foreach (var group in groups)
        {
            // 1. GET /dashboard/executive-summary
            group.MapGet("/executive-summary", async (
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

                var query = new GetDashboardExecutiveSummaryQuery(targetWorkspaceId.Value);
                var result = await dispatcher.QueryAsync(query, ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization()
            .WithName($"GetDashboardExecutiveSummary_{group.GetHashCode()}")
            .WithSummary("Retrieves current executive summary, key findings, risks, opportunities, and recommendations.");

            // 2. POST /dashboard/executive-summary/generate
            group.MapPost("/executive-summary/generate", async (
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
            .WithName($"GenerateDashboardExecutiveSummary_{group.GetHashCode()}")
            .WithSummary("Triggers on-demand multi-agent generation of an executive briefing.");

            // 3. GET /dashboard/alerts
            group.MapGet("/alerts", async (
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

                var query = new GetDashboardAlertsQuery(targetWorkspaceId.Value);
                var result = await dispatcher.QueryAsync(query, ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization()
            .WithName($"GetDashboardAlerts_{group.GetHashCode()}")
            .WithSummary("Retrieves overview of active alerts, severity breakdown, and latest incidents.");

            // 4. GET /dashboard/monitoring
            group.MapGet("/monitoring", async (
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

                var query = new GetDashboardMonitoringQuery(targetWorkspaceId.Value);
                var result = await dispatcher.QueryAsync(query, ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization()
            .WithName($"GetDashboardMonitoring_{group.GetHashCode()}")
            .WithSummary("Retrieves monitoring health percentage, active monitors, and recent background runs.");

            // 5. GET /dashboard/trends
            group.MapGet("/trends", async (
                [FromQuery] Guid? workspaceId,
                [FromQuery] int days,
                ITenantContext tenantContext,
                [FromServices] IDispatcher dispatcher,
                CancellationToken ct) =>
            {
                Guid? targetWorkspaceId = workspaceId ?? tenantContext.WorkspaceId;
                if (!targetWorkspaceId.HasValue)
                {
                    return Results.BadRequest(new { error = "Workspace ID is required." });
                }

                int effectiveDays = days > 0 ? days : 30;
                var query = new GetDashboardTrendsQuery(targetWorkspaceId.Value, effectiveDays);
                var result = await dispatcher.QueryAsync(query, ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization()
            .WithName($"GetDashboardTrends_{group.GetHashCode()}")
            .WithSummary("Retrieves time-series query trends, growth rate, and trajectory indicators.");

            // 6. GET /dashboard/anomalies
            group.MapGet("/anomalies", async (
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

                var query = new GetDashboardAnomaliesQuery(targetWorkspaceId.Value);
                var result = await dispatcher.QueryAsync(query, ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization()
            .WithName($"GetDashboardAnomalies_{group.GetHashCode()}")
            .WithSummary("Retrieves statistical anomalies detected via rolling Z-score analysis.");

            // 7. GET /dashboard/morning-brief (Phase D)
            group.MapGet("/morning-brief", async (
                [FromQuery] string? period,
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

                string? userName = user.FindFirstValue(ClaimTypes.GivenName)
                    ?? user.FindFirstValue(ClaimTypes.Name)
                    ?? user.FindFirstValue("name");

                var query = new GetMorningExecutiveBriefQuery(targetWorkspaceId.Value, period, userName);
                var result = await dispatcher.QueryAsync(query, ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization()
            .WithName($"GetMorningExecutiveBrief_{group.GetHashCode()}")
            .WithSummary("Retrieves personalized Morning Executive Brief with dynamic KPIs, prioritized anomalies, and ranked findings.");

            // 8. POST /dashboard/morning-brief/generate (Phase D)
            group.MapPost("/morning-brief/generate", async (
                [FromBody] GenerateMorningBriefRequestDto? request,
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

                string? userName = user.FindFirstValue(ClaimTypes.GivenName)
                    ?? user.FindFirstValue(ClaimTypes.Name)
                    ?? user.FindFirstValue("name");

                var command = new GenerateMorningExecutiveBriefCommand(
                    targetWorkspaceId.Value,
                    request?.Period ?? "Since yesterday",
                    userName);

                var result = await dispatcher.SendAsync(command, ct);
                return result.ToHttpResult(StatusCodes.Status201Created);
            })
            .RequireAuthorization()
            .WithName($"GenerateMorningExecutiveBrief_{group.GetHashCode()}")
            .WithSummary("Triggers on-demand multi-agent generation of a Morning Executive Brief for the specified period.");
        }

        return app;
    }
}
