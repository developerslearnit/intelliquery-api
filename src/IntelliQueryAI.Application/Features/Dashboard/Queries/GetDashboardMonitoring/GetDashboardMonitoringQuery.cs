using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Application.Features.Dashboard.Dtos;
using IntelliQueryAI.Application.Features.Monitoring.Dtos;
using IntelliQueryAI.Domain.Enums;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.SharedKernel.Results;

namespace IntelliQueryAI.Application.Features.Dashboard.Queries.GetDashboardMonitoring;

public sealed record GetDashboardMonitoringQuery(Guid WorkspaceId) : IQuery<Result<DashboardMonitoringResponseDto>>;

public sealed class GetDashboardMonitoringQueryHandler : IQueryHandler<GetDashboardMonitoringQuery, Result<DashboardMonitoringResponseDto>>
{
    private readonly IMonitorStore _monitorStore;

    public GetDashboardMonitoringQueryHandler(IMonitorStore monitorStore)
    {
        _monitorStore = monitorStore;
    }

    public async Task<Result<DashboardMonitoringResponseDto>> HandleAsync(
        GetDashboardMonitoringQuery query,
        CancellationToken cancellationToken = default)
    {
        var monitors = await _monitorStore.GetByWorkspaceIdAsync(query.WorkspaceId, cancellationToken);
        var recentExecutions = await _monitorStore.GetRecentExecutionsByWorkspaceIdAsync(query.WorkspaceId, limit: 10, cancellationToken);

        var monitorDtos = monitors.Select(m => new MonitorDto(
            m.Id,
            m.WorkspaceId,
            m.DataSourceId,
            m.Name,
            m.Description,
            m.MonitorType.ToString(),
            m.IsActive,
            m.ScheduleType.ToString(),
            m.QueryTemplate,
            m.TargetMetric,
            m.LastExecutedAtUtc,
            m.CreatedAtUtc,
            m.Rules.Select(r => new MonitorRuleDto(
                r.Id,
                r.MonitorId,
                r.RuleType.ToString(),
                r.ThresholdValue,
                r.ComparisonOperator.ToString(),
                r.TimeWindow,
                r.CreatedAtUtc)).ToList(),
            m.Executions.Select(e => new MonitorExecutionDto(
                e.Id,
                e.MonitorId,
                e.StartedAtUtc,
                e.CompletedAtUtc,
                e.Status.ToString(),
                e.CurrentMetricValue,
                e.BaselineMetricValue,
                e.Findings,
                e.GeneratedInsights,
                e.ErrorMessage,
                e.ExecutionDurationMs)).ToList())).ToList();

        var execDtos = recentExecutions.Select(e => new MonitorExecutionDto(
            e.Id,
            e.MonitorId,
            e.StartedAtUtc,
            e.CompletedAtUtc,
            e.Status.ToString(),
            e.CurrentMetricValue,
            e.BaselineMetricValue,
            e.Findings,
            e.GeneratedInsights,
            e.ErrorMessage,
            e.ExecutionDurationMs)).ToList();

        int total = monitors.Count;
        int active = monitors.Count(m => m.IsActive);
        var cutoff24h = DateTime.UtcNow.AddHours(-24);
        int exec24h = execDtos.Count(e => e.StartedAtUtc >= cutoff24h);

        // Healthy means either no executions yet or latest execution completed without failing
        int healthy = monitors.Count(m =>
        {
            var latest = m.Executions.OrderByDescending(e => e.StartedAtUtc).FirstOrDefault();
            return latest == null || latest.Status != ExecutionStatus.Failed;
        });

        double healthPercentage = total > 0
            ? Math.Round(((double)healthy / total) * 100.0, 1)
            : 100.0;

        return Result.Success(new DashboardMonitoringResponseDto(
            total,
            active,
            exec24h,
            healthy,
            healthPercentage,
            monitorDtos,
            execDtos));
    }
}
