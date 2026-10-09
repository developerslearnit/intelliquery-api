using IntelliQueryAI.SharedKernel.Results;

namespace IntelliQueryAI.Domain.Errors;

public static class MonitoringErrors
{
    public static readonly Error MonitorNotFound = Error.NotFound(
        "Monitoring.NotFound",
        "The requested AI Monitor was not found in the workspace.");

    public static readonly Error InvalidMonitorName = Error.Validation(
        "Monitoring.InvalidName",
        "Monitor name cannot be empty and must not exceed 200 characters.");

    public static readonly Error InvalidDataSource = Error.Validation(
        "Monitoring.InvalidDataSource",
        "A valid target data source is required to configure a monitor.");

    public static readonly Error EmptyRules = Error.Validation(
        "Monitoring.EmptyRules",
        "At least one monitor rule must be configured for the monitor.");

    public static readonly Error ExecutionFailed = Error.Failure(
        "Monitoring.ExecutionFailed",
        "Failed to execute the monitor against target data source.");

    public static readonly Error AlertNotFound = Error.NotFound(
        "Alerts.NotFound",
        "The requested alert was not found in the workspace.");

    public static readonly Error AlertAlreadyAcknowledged = Error.Conflict(
        "Alerts.AlreadyAcknowledged",
        "The alert has already been acknowledged.");

    public static readonly Error ExecutiveReportNotFound = Error.NotFound(
        "Executive.ReportNotFound",
        "The requested executive report was not found in the workspace.");

    public static readonly Error ExecutiveGenerationFailed = Error.Failure(
        "Executive.GenerationFailed",
        "Failed to generate executive intelligence summary from agent framework.");
}
