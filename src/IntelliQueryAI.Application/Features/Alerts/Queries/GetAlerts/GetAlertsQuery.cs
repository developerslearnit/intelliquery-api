using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Application.Features.Alerts.Dtos;
using IntelliQueryAI.Domain.Enums;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.SharedKernel.Results;

namespace IntelliQueryAI.Application.Features.Alerts.Queries.GetAlerts;

public sealed record GetAlertsQuery(
    Guid WorkspaceId,
    bool? OnlyOpen = null,
    AlertSeverity? Severity = null,
    int Limit = 100) : IQuery<Result<IReadOnlyList<AlertDto>>>;

public sealed class GetAlertsQueryHandler : IQueryHandler<GetAlertsQuery, Result<IReadOnlyList<AlertDto>>>
{
    private readonly IAlertStore _alertStore;

    public GetAlertsQueryHandler(IAlertStore alertStore)
    {
        _alertStore = alertStore;
    }

    public async Task<Result<IReadOnlyList<AlertDto>>> HandleAsync(
        GetAlertsQuery query,
        CancellationToken cancellationToken = default)
    {
        var alerts = await _alertStore.GetByWorkspaceIdAsync(
            query.WorkspaceId,
            query.OnlyOpen,
            query.Severity,
            query.Limit,
            cancellationToken);

        var dtos = alerts.Select(a => new AlertDto(
            a.Id,
            a.WorkspaceId,
            a.MonitorId,
            a.AlertType.ToString(),
            a.Severity.ToString(),
            a.Title,
            a.Description,
            a.MetricValue,
            a.ThresholdValue,
            a.IsAcknowledged,
            a.AcknowledgedAtUtc,
            a.AcknowledgedByUserId,
            a.CreatedAtUtc)).ToList();

        return Result.Success<IReadOnlyList<AlertDto>>(dtos);
    }
}
