using IntelliQueryAI.Domain.Enums;
using IntelliQueryAI.SharedKernel.Primitives;

namespace IntelliQueryAI.Domain.Entities.Alerts;

/// <summary>
/// Proactive intelligent alert triggered by monitor thresholds, statistical anomalies, or automated agent checks.
/// </summary>
public sealed class Alert : AggregateRoot
{
    public Guid WorkspaceId { get; private set; }
    public Guid? MonitorId { get; private set; }
    public AlertType AlertType { get; private set; }
    public AlertSeverity Severity { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public decimal? MetricValue { get; private set; }
    public decimal? ThresholdValue { get; private set; }
    public bool IsAcknowledged { get; private set; }
    public DateTime? AcknowledgedAtUtc { get; private set; }
    public Guid? AcknowledgedByUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private Alert(
        Guid workspaceId,
        Guid? monitorId,
        AlertType alertType,
        AlertSeverity severity,
        string title,
        string description,
        decimal? metricValue,
        decimal? thresholdValue)
        : base(Guid.NewGuid())
    {
        WorkspaceId = workspaceId;
        MonitorId = monitorId;
        AlertType = alertType;
        Severity = severity;
        Title = title;
        Description = description;
        MetricValue = metricValue;
        ThresholdValue = thresholdValue;
        IsAcknowledged = false;
        CreatedAtUtc = DateTime.UtcNow;
    }

    private Alert() { }

    public static Alert Create(
        Guid workspaceId,
        Guid? monitorId,
        AlertType alertType,
        AlertSeverity severity,
        string title,
        string description,
        decimal? metricValue = null,
        decimal? thresholdValue = null)
    {
        return new Alert(
            workspaceId,
            monitorId,
            alertType,
            severity,
            title,
            description,
            metricValue,
            thresholdValue);
    }

    public void Acknowledge(Guid userId)
    {
        IsAcknowledged = true;
        AcknowledgedAtUtc = DateTime.UtcNow;
        AcknowledgedByUserId = userId;
    }
}
