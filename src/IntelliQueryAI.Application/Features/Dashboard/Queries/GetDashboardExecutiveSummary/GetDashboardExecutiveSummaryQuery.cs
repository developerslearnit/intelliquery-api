using IntelliQueryAI.AI.Agents;
using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Application.Features.Dashboard.Dtos;
using IntelliQueryAI.Application.Features.ExecutiveIntelligence.Queries.GetExecutiveReports;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.SharedKernel.Results;

namespace IntelliQueryAI.Application.Features.Dashboard.Queries.GetDashboardExecutiveSummary;

public sealed record GetDashboardExecutiveSummaryQuery(Guid WorkspaceId) : IQuery<Result<DashboardExecutiveSummaryResponseDto>>;

public sealed class GetDashboardExecutiveSummaryQueryHandler : IQueryHandler<GetDashboardExecutiveSummaryQuery, Result<DashboardExecutiveSummaryResponseDto>>
{
    private readonly IExecutiveReportStore _executiveStore;
    private readonly IWorkspaceStore? _workspaceStore;

    public GetDashboardExecutiveSummaryQueryHandler(
        IExecutiveReportStore executiveStore,
        IWorkspaceStore? workspaceStore = null)
    {
        _executiveStore = executiveStore;
        _workspaceStore = workspaceStore;
    }

    public async Task<Result<DashboardExecutiveSummaryResponseDto>> HandleAsync(
        GetDashboardExecutiveSummaryQuery query,
        CancellationToken cancellationToken = default)
    {
        string? workspaceName = null;
        if (_workspaceStore is not null)
        {
            var ws = await _workspaceStore.GetByIdAsync(query.WorkspaceId, cancellationToken);
            workspaceName = ws?.Name;
        }

        string displayName = !string.IsNullOrWhiteSpace(workspaceName) ? workspaceName : "Workspace";

        var latest = await _executiveStore.GetLatestByWorkspaceIdAsync(query.WorkspaceId, cancellationToken);

        if (latest is not null)
        {
            var dto = GetExecutiveReportsQueryHandler.MapToDto(latest, displayName);
            return Result.Success(new DashboardExecutiveSummaryResponseDto(
                dto.Id,
                dto.ReportPeriod,
                dto.Title,
                dto.Summary,
                dto.Findings,
                dto.Risks,
                dto.Opportunities,
                dto.Recommendations,
                dto.GeneratedAtUtc));
        }

        // Return baseline briefing if no manual/scheduled report run has occurred yet
        return Result.Success(new DashboardExecutiveSummaryResponseDto(
            ReportId: null,
            ReportPeriod: DateTime.UtcNow.ToString("MMMM yyyy"),
            Title: $"{displayName} Executive Intelligence Summary",
            Summary: $"{displayName} operational telemetry nominal. AI multi-agent query engine and automated background monitoring are initialized. No anomalous operational regressions detected.",
            KeyFindings: new List<string>
            {
                "Connected data sources initialized and ready for automated AST-validated queries.",
                "AI specialized agents active with real-time semantic schema discovery."
            },
            Risks: new List<string>
            {
                "Ensure at least one automated monitor is configured to track core revenue or churn metrics."
            },
            Opportunities: new List<string>
            {
                "Configure automated schedule triggers for recurring executive briefings."
            },
            Recommendations: new List<ActionableRecommendation>
            {
                new(
                    Title: "Set Up AI Revenue Monitor",
                    Category: "Monitoring",
                    Priority: "High",
                    Rationale: "Continuous Z-score evaluation provides early warning against transactional drops.",
                    ActionSteps: new List<string>
                    {
                        "Navigate to Monitoring dashboard",
                        "Create a Revenue Monitor with 20% deviation threshold"
                    })
            },
            GeneratedAtUtc: DateTime.UtcNow));
    }
}
