using IntelliQueryAI.Domain.Enums;

namespace IntelliQueryAI.Application.Features.Alerts.Dtos;

public sealed record AlertDto(
    Guid Id,
    Guid WorkspaceId,
    Guid? MonitorId,
    string AlertType,
    string Severity,
    string Title,
    string Description,
    decimal? MetricValue,
    decimal? ThresholdValue,
    bool IsAcknowledged,
    DateTime? AcknowledgedAtUtc,
    Guid? AcknowledgedByUserId,
    DateTime CreatedAtUtc);

public sealed record AcknowledgeAlertRequest(Guid AlertId);

public sealed record CreateAlertRequest(
    Guid? MonitorId,
    AlertType AlertType,
    AlertSeverity Severity,
    string Title,
    string Description,
    decimal? MetricValue = null,
    decimal? ThresholdValue = null);

public sealed record OpenAlertsSummaryDto(
    int TotalOpenCount,
    int CriticalCount,
    int WarningCount,
    int InformationCount,
    IReadOnlyList<AlertDto> Alerts);
