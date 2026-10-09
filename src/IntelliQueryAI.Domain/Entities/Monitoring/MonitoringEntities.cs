using IntelliQueryAI.Domain.Enums;
using IntelliQueryAI.SharedKernel.Primitives;

namespace IntelliQueryAI.Domain.Entities.Monitoring;

/// <summary>
/// Configured automated monitor that periodically audits metrics and executes analytical rules.
/// </summary>
public sealed class Monitor : AggregateRoot
{
    private readonly List<MonitorRule> _rules = [];
    private readonly List<MonitorExecution> _executions = [];

    public Guid WorkspaceId { get; private set; }
    public Guid DataSourceId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public MonitorType MonitorType { get; private set; }
    public bool IsActive { get; private set; }
    public ScheduleType ScheduleType { get; private set; }
    public string? QueryTemplate { get; private set; }
    public string? TargetMetric { get; private set; }
    public DateTime? LastExecutedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    public IReadOnlyCollection<MonitorRule> Rules => _rules.AsReadOnly();
    public IReadOnlyCollection<MonitorExecution> Executions => _executions.AsReadOnly();

    private Monitor(
        Guid workspaceId,
        Guid dataSourceId,
        string name,
        string? description,
        MonitorType monitorType,
        ScheduleType scheduleType,
        string? queryTemplate,
        string? targetMetric)
        : base(Guid.NewGuid())
    {
        WorkspaceId = workspaceId;
        DataSourceId = dataSourceId;
        Name = name;
        Description = description;
        MonitorType = monitorType;
        IsActive = true;
        ScheduleType = scheduleType;
        QueryTemplate = queryTemplate;
        TargetMetric = targetMetric;
        CreatedAtUtc = DateTime.UtcNow;
    }

    private Monitor() { }

    public static Monitor Create(
        Guid workspaceId,
        Guid dataSourceId,
        string name,
        string? description,
        MonitorType monitorType,
        ScheduleType scheduleType,
        string? queryTemplate = null,
        string? targetMetric = null)
    {
        return new Monitor(
            workspaceId,
            dataSourceId,
            name,
            description,
            monitorType,
            scheduleType,
            queryTemplate,
            targetMetric);
    }

    public void Update(
        string name,
        string? description,
        MonitorType monitorType,
        ScheduleType scheduleType,
        string? queryTemplate,
        string? targetMetric)
    {
        Name = name;
        Description = description;
        MonitorType = monitorType;
        ScheduleType = scheduleType;
        QueryTemplate = queryTemplate;
        TargetMetric = targetMetric;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void AddRule(MonitorRule rule)
    {
        _rules.Add(rule);
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void RemoveRule(Guid ruleId)
    {
        _rules.RemoveAll(r => r.Id == ruleId);
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void ClearRules()
    {
        _rules.Clear();
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void RecordExecution(MonitorExecution execution)
    {
        _executions.Add(execution);
        LastExecutedAtUtc = execution.StartedAtUtc;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}

/// <summary>
/// Quantitative threshold rule or statistical invariant evaluated by a monitor.
/// </summary>
public sealed class MonitorRule : Entity
{
    public Guid MonitorId { get; private set; }
    public RuleType RuleType { get; private set; }
    public decimal ThresholdValue { get; private set; }
    public ComparisonOperator ComparisonOperator { get; private set; }
    public string? TimeWindow { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private MonitorRule(
        Guid monitorId,
        RuleType ruleType,
        decimal thresholdValue,
        ComparisonOperator comparisonOperator,
        string? timeWindow)
        : base(Guid.NewGuid())
    {
        MonitorId = monitorId;
        RuleType = ruleType;
        ThresholdValue = thresholdValue;
        ComparisonOperator = comparisonOperator;
        TimeWindow = timeWindow ?? "30d";
        CreatedAtUtc = DateTime.UtcNow;
    }

    private MonitorRule() { }

    public static MonitorRule Create(
        Guid monitorId,
        RuleType ruleType,
        decimal thresholdValue,
        ComparisonOperator comparisonOperator,
        string? timeWindow = "30d")
    {
        return new MonitorRule(monitorId, ruleType, thresholdValue, comparisonOperator, timeWindow);
    }

    /// <summary>
    /// Evaluates if the current metric value violates this rule.
    /// Returns true if rule is triggered (an alert condition is met).
    /// </summary>
    public bool Evaluate(decimal currentValue, decimal? baselineValue = null)
    {
        decimal comparisonTarget = RuleType switch
        {
            RuleType.Threshold => currentValue,
            RuleType.PercentageChange when baselineValue.HasValue && baselineValue.Value != 0 =>
                ((currentValue - baselineValue.Value) / Math.Abs(baselineValue.Value)) * 100m,
            RuleType.PercentageChange => 0m,
            RuleType.AnomalyZScore => currentValue, // currentValue holds z-score
            RuleType.AbsenceOfData => currentValue == 0 ? 1m : 0m,
            _ => currentValue
        };

        if (RuleType == RuleType.AbsenceOfData)
        {
            return currentValue == 0;
        }

        return ComparisonOperator switch
        {
            ComparisonOperator.GreaterThan => comparisonTarget > ThresholdValue,
            ComparisonOperator.GreaterThanOrEqual => comparisonTarget >= ThresholdValue,
            ComparisonOperator.LessThan => comparisonTarget < ThresholdValue,
            ComparisonOperator.LessThanOrEqual => comparisonTarget <= ThresholdValue,
            ComparisonOperator.Equal => Math.Abs(comparisonTarget - ThresholdValue) < 0.0001m,
            ComparisonOperator.NotEqual => Math.Abs(comparisonTarget - ThresholdValue) >= 0.0001m,
            _ => false
        };
    }
}

/// <summary>
/// Historical record of an individual execution run for an AI Monitor.
/// </summary>
public sealed class MonitorExecution : Entity
{
    public Guid MonitorId { get; private set; }
    public DateTime StartedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public ExecutionStatus Status { get; private set; }
    public decimal? CurrentMetricValue { get; private set; }
    public decimal? BaselineMetricValue { get; private set; }
    public string? Findings { get; private set; }
    public string? GeneratedInsights { get; private set; }
    public string? ErrorMessage { get; private set; }
    public long ExecutionDurationMs { get; private set; }

    private MonitorExecution(Guid monitorId)
        : base(Guid.NewGuid())
    {
        MonitorId = monitorId;
        StartedAtUtc = DateTime.UtcNow;
        Status = ExecutionStatus.Running;
    }

    private MonitorExecution() { }

    public static MonitorExecution Start(Guid monitorId)
    {
        return new MonitorExecution(monitorId);
    }

    public void Complete(
        decimal? currentValue,
        decimal? baselineValue,
        string? findings,
        string? generatedInsights,
        bool alertTriggered,
        long durationMs)
    {
        CurrentMetricValue = currentValue;
        BaselineMetricValue = baselineValue;
        Findings = findings;
        GeneratedInsights = generatedInsights;
        Status = alertTriggered ? ExecutionStatus.TriggeredAlert : ExecutionStatus.Completed;
        CompletedAtUtc = DateTime.UtcNow;
        ExecutionDurationMs = durationMs;
    }

    public void Fail(string errorMessage, long durationMs)
    {
        ErrorMessage = errorMessage;
        Status = ExecutionStatus.Failed;
        CompletedAtUtc = DateTime.UtcNow;
        ExecutionDurationMs = durationMs;
    }
}
