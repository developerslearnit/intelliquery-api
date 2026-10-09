using System.Text.Json;
using FluentValidation;
using IntelliQueryAI.AI.Coordinator;
using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Application.Features.Dashboard.Dtos;
using IntelliQueryAI.Application.Features.Dashboard.Queries.GetMorningExecutiveBrief;
using IntelliQueryAI.Domain.Entities.Executive;
using IntelliQueryAI.Domain.Errors;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.Domain.Services;
using IntelliQueryAI.SharedKernel.Results;

namespace IntelliQueryAI.Application.Features.Dashboard.Commands.GenerateMorningExecutiveBrief;

public sealed record GenerateMorningExecutiveBriefCommand(
    Guid WorkspaceId,
    string? Period = "Since yesterday",
    string? UserName = null) : ICommand<Result<MorningExecutiveBriefDto>>;

public sealed class GenerateMorningExecutiveBriefCommandValidator : AbstractValidator<GenerateMorningExecutiveBriefCommand>
{
    public GenerateMorningExecutiveBriefCommandValidator()
    {
        RuleFor(x => x.WorkspaceId).NotEmpty();
    }
}

public sealed class GenerateMorningExecutiveBriefCommandHandler : ICommandHandler<GenerateMorningExecutiveBriefCommand, Result<MorningExecutiveBriefDto>>
{
    private readonly IExecutiveCoordinator _executiveCoordinator;
    private readonly IExecutiveReportStore _executiveReportStore;
    private readonly IMetricStore _metricStore;
    private readonly IMonitorStore _monitorStore;
    private readonly IAlertStore _alertStore;
    private readonly IQueryHistoryStore _queryHistoryStore;
    private readonly IWorkspaceStore? _workspaceStore;
    private readonly ITenantContext _tenantContext;
    private readonly IDispatcher _dispatcher;

    public GenerateMorningExecutiveBriefCommandHandler(
        IExecutiveCoordinator executiveCoordinator,
        IExecutiveReportStore executiveReportStore,
        IMetricStore metricStore,
        IMonitorStore monitorStore,
        IAlertStore alertStore,
        IQueryHistoryStore queryHistoryStore,
        ITenantContext tenantContext,
        IDispatcher dispatcher,
        IWorkspaceStore? workspaceStore = null)
    {
        _executiveCoordinator = executiveCoordinator;
        _executiveReportStore = executiveReportStore;
        _metricStore = metricStore;
        _monitorStore = monitorStore;
        _alertStore = alertStore;
        _queryHistoryStore = queryHistoryStore;
        _tenantContext = tenantContext;
        _dispatcher = dispatcher;
        _workspaceStore = workspaceStore;
    }

    public async Task<Result<MorningExecutiveBriefDto>> HandleAsync(
        GenerateMorningExecutiveBriefCommand command,
        CancellationToken cancellationToken = default)
    {
        // 1. Tenant Isolation Verification
        if (_tenantContext.WorkspaceId.HasValue && _tenantContext.WorkspaceId.Value != command.WorkspaceId)
        {
            return Result.Failure<MorningExecutiveBriefDto>(AiErrors.ContextTenantMismatch);
        }

        string period = string.IsNullOrWhiteSpace(command.Period) ? "Since yesterday" : command.Period.Trim();

        string? workspaceName = null;
        if (_workspaceStore is not null)
        {
            var ws = await _workspaceStore.GetByIdAsync(command.WorkspaceId, cancellationToken);
            workspaceName = ws?.Name;
        }

        string displayName = !string.IsNullOrWhiteSpace(workspaceName) ? workspaceName : "Workspace";
        string title = $"{displayName} Executive Briefing - {period}";

        // 2. Gather Dynamic Business Metrics from Semantic Layer and Monitors (No Hardcoded Metrics)
        var semanticMetrics = await _metricStore.GetByWorkspaceIdAsync(command.WorkspaceId, isActive: true, cancellationToken: cancellationToken);
        var activeMonitors = await _monitorStore.GetByWorkspaceIdAsync(command.WorkspaceId, cancellationToken: cancellationToken);
        var recentExecutions = await _monitorStore.GetRecentExecutionsByWorkspaceIdAsync(command.WorkspaceId, limit: 50, cancellationToken: cancellationToken);
        var recentAlerts = await _alertStore.GetByWorkspaceIdAsync(command.WorkspaceId, onlyOpen: true, limit: 10, cancellationToken: cancellationToken);
        var queryHistories = await _queryHistoryStore.GetByWorkspaceIdAsync(command.WorkspaceId, limit: 100, cancellationToken: cancellationToken);

        var keyMetrics = new Dictionary<string, decimal>();

        foreach (var m in activeMonitors.Where(m => m.IsActive))
        {
            var exec = recentExecutions.FirstOrDefault(e => e.MonitorId == m.Id);
            if (exec?.CurrentMetricValue.HasValue == true)
            {
                keyMetrics[m.TargetMetric ?? m.Name] = exec.CurrentMetricValue.Value;
            }
        }

        foreach (var sm in semanticMetrics)
        {
            if (!keyMetrics.ContainsKey(sm.Name))
            {
                keyMetrics[sm.Name] = 0m;
            }
        }

        keyMetrics["RecordedQueriesVolume"] = queryHistories.Count;
        keyMetrics["ActiveMonitorsCount"] = activeMonitors.Count(m => m.IsActive);
        keyMetrics["OpenAlertsCount"] = recentAlerts.Count;

        var alertSummaries = recentAlerts.Select(a => $"[{a.Severity}] {a.Title}").ToList();
        var monitorSummaries = activeMonitors.Select(m => $"{m.Name} ({m.MonitorType}, Active: {m.IsActive})").ToList();

        var agentReq = new GenerateExecutiveReportRequest(
            command.WorkspaceId,
            period,
            title,
            keyMetrics,
            alertSummaries,
            monitorSummaries,
            WorkspaceName: displayName);

        var agentResult = await _executiveCoordinator.GenerateExecutiveBriefingAsync(agentReq, cancellationToken);
        if (!agentResult.IsSuccess)
        {
            return Result.Failure<MorningExecutiveBriefDto>(agentResult.Error);
        }

        var brief = agentResult.Value;

        var executiveReport = ExecutiveReport.Create(
            command.WorkspaceId,
            brief.Title,
            brief.ReportPeriod,
            brief.Summary,
            JsonSerializer.Serialize(brief.KeyFindings),
            JsonSerializer.Serialize(brief.Risks),
            JsonSerializer.Serialize(brief.Opportunities),
            JsonSerializer.Serialize(brief.Recommendations));

        await _executiveReportStore.AddAsync(executiveReport, cancellationToken);

        // 3. Return full MorningExecutiveBriefDto via Query
        var query = new GetMorningExecutiveBriefQuery(command.WorkspaceId, period, command.UserName);
        return await _dispatcher.QueryAsync(query, cancellationToken);
    }
}
