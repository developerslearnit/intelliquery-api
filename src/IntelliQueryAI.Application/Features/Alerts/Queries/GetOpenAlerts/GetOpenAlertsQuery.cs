using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Application.Features.Alerts.Dtos;
using IntelliQueryAI.Domain.Enums;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.SharedKernel.Results;

namespace IntelliQueryAI.Application.Features.Alerts.Queries.GetOpenAlerts;

public sealed record GetOpenAlertsQuery(Guid WorkspaceId) : IQuery<Result<OpenAlertsSummaryDto>>;

public sealed class GetOpenAlertsQueryHandler : IQueryHandler<GetOpenAlertsQuery, Result<OpenAlertsSummaryDto>>
{
    private readonly IAlertStore _alertStore;

    public GetOpenAlertsQueryHandler(IAlertStore alertStore)
    {
        _alertStore = alertStore;
    }

    public async Task<Result<OpenAlertsSummaryDto>> HandleAsync(
        GetOpenAlertsQuery query,
        CancellationToken cancellationToken = default)
    {
        var openAlerts = await _alertStore.GetByWorkspaceIdAsync(
            query.WorkspaceId,
            onlyOpen: true,
            severity: null,
            limit: 50,
            cancellationToken: cancellationToken);

        var dtos = openAlerts.Select(a => new AlertDto(
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

        int critical = dtos.Count(a => a.Severity == AlertSeverity.Critical.ToString());
        int warning = dtos.Count(a => a.Severity == AlertSeverity.Warning.ToString());
        int info = dtos.Count(a => a.Severity == AlertSeverity.Information.ToString());

        var result = new OpenAlertsSummaryDto(
            dtos.Count,
            critical,
            warning,
            info,
            dtos);

        return Result.Success(result);
    }
}
