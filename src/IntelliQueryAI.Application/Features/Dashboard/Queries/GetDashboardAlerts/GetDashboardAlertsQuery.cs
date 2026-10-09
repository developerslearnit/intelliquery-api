using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Application.Features.Alerts.Dtos;
using IntelliQueryAI.Application.Features.Dashboard.Dtos;
using IntelliQueryAI.Domain.Enums;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.SharedKernel.Results;

namespace IntelliQueryAI.Application.Features.Dashboard.Queries.GetDashboardAlerts;

public sealed record GetDashboardAlertsQuery(Guid WorkspaceId) : IQuery<Result<DashboardAlertsResponseDto>>;

public sealed class GetDashboardAlertsQueryHandler : IQueryHandler<GetDashboardAlertsQuery, Result<DashboardAlertsResponseDto>>
{
    private readonly IAlertStore _alertStore;

    public GetDashboardAlertsQueryHandler(IAlertStore alertStore)
    {
        _alertStore = alertStore;
    }

    public async Task<Result<DashboardAlertsResponseDto>> HandleAsync(
        GetDashboardAlertsQuery query,
        CancellationToken cancellationToken = default)
    {
        var alerts = await _alertStore.GetByWorkspaceIdAsync(query.WorkspaceId, onlyOpen: null, limit: 10, cancellationToken: cancellationToken);

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

        var openAlerts = dtos.Where(a => !a.IsAcknowledged).ToList();
        int critical = openAlerts.Count(a => a.Severity == AlertSeverity.Critical.ToString());
        int warning = openAlerts.Count(a => a.Severity == AlertSeverity.Warning.ToString());
        int info = openAlerts.Count(a => a.Severity == AlertSeverity.Information.ToString());

        return Result.Success(new DashboardAlertsResponseDto(
            openAlerts.Count,
            critical,
            warning,
            info,
            dtos));
    }
}
