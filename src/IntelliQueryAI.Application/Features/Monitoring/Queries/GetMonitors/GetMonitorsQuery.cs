using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Application.Features.Monitoring.Dtos;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.SharedKernel.Results;

namespace IntelliQueryAI.Application.Features.Monitoring.Queries.GetMonitors;

public sealed record GetMonitorsQuery(Guid WorkspaceId) : IQuery<Result<IReadOnlyList<MonitorDto>>>;

public sealed class GetMonitorsQueryHandler : IQueryHandler<GetMonitorsQuery, Result<IReadOnlyList<MonitorDto>>>
{
    private readonly IMonitorStore _monitorStore;

    public GetMonitorsQueryHandler(IMonitorStore monitorStore)
    {
        _monitorStore = monitorStore;
    }

    public async Task<Result<IReadOnlyList<MonitorDto>>> HandleAsync(
        GetMonitorsQuery query,
        CancellationToken cancellationToken = default)
    {
        var monitors = await _monitorStore.GetByWorkspaceIdAsync(query.WorkspaceId, cancellationToken);

        var dtos = monitors.Select(m => new MonitorDto(
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

        return Result.Success<IReadOnlyList<MonitorDto>>(dtos);
    }
}
