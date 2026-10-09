namespace IntelliQueryAI.Domain.Enums;

/// <summary>
/// Type of metric or operational domain monitored by an AI Monitor.
/// </summary>
public enum MonitorType
{
    Revenue = 0,
    TaxCollection = 1,
    CustomerChurn = 2,
    FailedPayment = 3,
    SubscriptionGrowth = 4,
    Expense = 5,
    Custom = 6
}

/// <summary>
/// Execution cadence for automated AI monitor checks.
/// </summary>
public enum ScheduleType
{
    Hourly = 0,
    Daily = 1,
    Weekly = 2,
    Monthly = 3
}

/// <summary>
/// Evaluation logic applied against monitor metrics.
/// </summary>
public enum RuleType
{
    Threshold = 0,
    PercentageChange = 1,
    AnomalyZScore = 2,
    AbsenceOfData = 3
}

/// <summary>
/// Comparison operator evaluated against rule thresholds.
/// </summary>
public enum ComparisonOperator
{
    GreaterThan = 0,
    GreaterThanOrEqual = 1,
    LessThan = 2,
    LessThanOrEqual = 3,
    Equal = 4,
    NotEqual = 5
}

/// <summary>
/// State of a background or on-demand monitor run.
/// </summary>
public enum ExecutionStatus
{
    Pending = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
    TriggeredAlert = 4
}

/// <summary>
/// Classification of triggered alerts.
/// </summary>
public enum AlertType
{
    ThresholdExceeded = 0,
    AnomalyDetected = 1,
    InactivityWarning = 2,
    DropSpike = 3,
    System = 4
}

/// <summary>
/// Urgency and impact level of an alert.
/// </summary>
public enum AlertSeverity
{
    Information = 1,
    Warning = 2,
    Critical = 3
}
