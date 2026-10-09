using System.Text.Json;
using FluentValidation;
using IntelliQueryAI.AI.Agents;
using IntelliQueryAI.AI.Coordinator;
using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Application.Features.ExecutiveIntelligence.Dtos;
using IntelliQueryAI.Domain.Entities.Audit;
using IntelliQueryAI.Domain.Entities.Executive;
using IntelliQueryAI.Domain.Errors;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.SharedKernel.Results;

namespace IntelliQueryAI.Application.Features.ExecutiveIntelligence.Commands.GenerateExecutiveReport;

public sealed record GenerateExecutiveReportCommand(
    Guid WorkspaceId,
    Guid UserId,
    string? ReportPeriod = null,
    string? CustomTitle = null) : ICommand<Result<ExecutiveReportDto>>;

public sealed class GenerateExecutiveReportCommandValidator : AbstractValidator<GenerateExecutiveReportCommand>
{
    public GenerateExecutiveReportCommandValidator()
    {
        RuleFor(x => x.WorkspaceId).NotEmpty();
    }
}

public sealed class GenerateExecutiveReportCommandHandler : ICommandHandler<GenerateExecutiveReportCommand, Result<ExecutiveReportDto>>
{
    private readonly IExecutiveCoordinator _executiveCoordinator;
    private readonly IExecutiveReportStore _executiveReportStore;
    private readonly IQueryHistoryStore _queryHistoryStore;
    private readonly IMonitorStore _monitorStore;
    private readonly IAlertStore _alertStore;
    private readonly IAuditLogStore _auditLogStore;
    private readonly IWorkspaceStore? _workspaceStore;
    private readonly IUnitOfWork _unitOfWork;

    public GenerateExecutiveReportCommandHandler(
        IExecutiveCoordinator executiveCoordinator,
        IExecutiveReportStore executiveReportStore,
        IQueryHistoryStore queryHistoryStore,
        IMonitorStore monitorStore,
        IAlertStore alertStore,
        IAuditLogStore auditLogStore,
        IUnitOfWork unitOfWork,
        IWorkspaceStore? workspaceStore = null)
    {
        _executiveCoordinator = executiveCoordinator;
        _executiveReportStore = executiveReportStore;
        _queryHistoryStore = queryHistoryStore;
        _monitorStore = monitorStore;
        _alertStore = alertStore;
        _auditLogStore = auditLogStore;
        _unitOfWork = unitOfWork;
        _workspaceStore = workspaceStore;
    }

    public async Task<Result<ExecutiveReportDto>> HandleAsync(
        GenerateExecutiveReportCommand command,
        CancellationToken cancellationToken = default)
    {
        string period = command.ReportPeriod ?? DateTime.UtcNow.ToString("MMMM yyyy");

        string? workspaceName = null;
        if (_workspaceStore is not null)
        {
            var ws = await _workspaceStore.GetByIdAsync(command.WorkspaceId, cancellationToken);
            workspaceName = ws?.Name;
        }

        string displayName = !string.IsNullOrWhiteSpace(workspaceName) ? workspaceName : "Workspace";
        string title = command.CustomTitle ?? $"{displayName} Executive Briefing - {period}";

        var recentQueries = await _queryHistoryStore.GetByWorkspaceIdAsync(command.WorkspaceId, limit: 100, cancellationToken: cancellationToken);
        var activeMonitors = await _monitorStore.GetByWorkspaceIdAsync(command.WorkspaceId, cancellationToken: cancellationToken);
        var recentAlerts = await _alertStore.GetByWorkspaceIdAsync(command.WorkspaceId, limit: 10, cancellationToken: cancellationToken);

        var keyMetrics = new Dictionary<string, decimal>
        {
            ["TotalRecordedQueries"] = recentQueries.Count,
            ["SuccessfulQueries"] = recentQueries.Count(q => q.IsSuccessful),
            ["ActiveMonitorsCount"] = activeMonitors.Count(m => m.IsActive),
            ["OpenAlertsCount"] = recentAlerts.Count(a => !a.IsAcknowledged)
        };

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
        if (agentResult.IsFailure)
        {
            return Result.Failure<ExecutiveReportDto>(agentResult.Error);
        }

        var briefing = agentResult.Value;

        var report = ExecutiveReport.Create(
            command.WorkspaceId,
            briefing.Title,
            briefing.ReportPeriod,
            briefing.Summary,
            JsonSerializer.Serialize(briefing.KeyFindings),
            JsonSerializer.Serialize(briefing.Risks),
            JsonSerializer.Serialize(briefing.Opportunities),
            JsonSerializer.Serialize(briefing.Recommendations));

        await _executiveReportStore.AddAsync(report, cancellationToken);

        await _auditLogStore.AddAsync(AuditLog.Create(
            command.WorkspaceId,
            command.UserId,
            "Executive.GenerateBriefing",
            "ExecutiveReport",
            report.Id.ToString(),
            $"Generated Executive Intelligence Report '{report.Title}' for {report.ReportPeriod}."),
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var dto = new ExecutiveReportDto(
            report.Id,
            report.WorkspaceId,
            report.Title,
            report.ReportPeriod,
            report.Summary,
            briefing.KeyFindings,
            briefing.Risks,
            briefing.Opportunities,
            briefing.Recommendations,
            report.GeneratedAtUtc);

        return Result.Success(dto);
    }
}
