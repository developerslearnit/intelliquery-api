using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Application.Features.Monitoring.Dtos;
using IntelliQueryAI.Domain.Errors;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.SharedKernel.Results;

namespace IntelliQueryAI.Application.Features.Monitoring.Queries.GetMonitorById;

public sealed record GetMonitorByIdQuery(Guid WorkspaceId, Guid MonitorId) : IQuery<Result<MonitorDto>>;

public sealed class GetMonitorByIdQueryHandler : IQueryHandler<GetMonitorByIdQuery, Result<MonitorDto>>
{
    private readonly IMonitorStore _monitorStore;

    public GetMonitorByIdQueryHandler(IMonitorStore monitorStore)
    {
        _monitorStore = monitorStore;
    }

    public async Task<Result<MonitorDto>> HandleAsync(
        GetMonitorByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        var m = await _monitorStore.GetByIdAsync(query.MonitorId, cancellationToken);
        if (m is null || m.WorkspaceId != query.WorkspaceId)
        {
            return Result.Failure<MonitorDto>(MonitoringErrors.MonitorNotFound);
        }

        var dto = new MonitorDto(
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
                e.ExecutionDurationMs)).ToList());

        return Result.Success(dto);
    }
}
