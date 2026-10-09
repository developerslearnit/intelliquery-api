using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Application.Features.Monitoring.Dtos;
using IntelliQueryAI.Domain.Errors;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.SharedKernel.Results;

namespace IntelliQueryAI.Application.Features.Monitoring.Queries.GetMonitorExecutions;

public sealed record GetMonitorExecutionsQuery(
    Guid WorkspaceId,
    Guid MonitorId,
    int Limit = 50) : IQuery<Result<IReadOnlyList<MonitorExecutionDto>>>;

public sealed class GetMonitorExecutionsQueryHandler : IQueryHandler<GetMonitorExecutionsQuery, Result<IReadOnlyList<MonitorExecutionDto>>>
{
    private readonly IMonitorStore _monitorStore;

    public GetMonitorExecutionsQueryHandler(IMonitorStore monitorStore)
    {
        _monitorStore = monitorStore;
    }

    public async Task<Result<IReadOnlyList<MonitorExecutionDto>>> HandleAsync(
        GetMonitorExecutionsQuery query,
        CancellationToken cancellationToken = default)
    {
        var monitor = await _monitorStore.GetByIdAsync(query.MonitorId, cancellationToken);
        if (monitor is null || monitor.WorkspaceId != query.WorkspaceId)
        {
            return Result.Failure<IReadOnlyList<MonitorExecutionDto>>(MonitoringErrors.MonitorNotFound);
        }

        var executions = await _monitorStore.GetExecutionsByMonitorIdAsync(query.MonitorId, query.Limit, cancellationToken);

        var dtos = executions.Select(e => new MonitorExecutionDto(
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

        return Result.Success<IReadOnlyList<MonitorExecutionDto>>(dtos);
    }
}
