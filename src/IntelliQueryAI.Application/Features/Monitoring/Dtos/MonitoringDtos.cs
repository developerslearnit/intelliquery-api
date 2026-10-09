using IntelliQueryAI.Domain.Enums;

namespace IntelliQueryAI.Application.Features.Monitoring.Dtos;

public sealed record MonitorRuleDto(
    Guid Id,
    Guid MonitorId,
    string RuleType,
    decimal ThresholdValue,
    string ComparisonOperator,
    string? TimeWindow,
    DateTime CreatedAtUtc);

public sealed record MonitorExecutionDto(
    Guid Id,
    Guid MonitorId,
    DateTime StartedAtUtc,
    DateTime? CompletedAtUtc,
    string Status,
    decimal? CurrentMetricValue,
    decimal? BaselineMetricValue,
    string? Findings,
    string? GeneratedInsights,
    string? ErrorMessage,
    long ExecutionDurationMs);

public sealed record MonitorDto(
    Guid Id,
    Guid WorkspaceId,
    Guid DataSourceId,
    string Name,
    string? Description,
    string MonitorType,
    bool IsActive,
    string ScheduleType,
    string? QueryTemplate,
    string? TargetMetric,
    DateTime? LastExecutedAtUtc,
    DateTime CreatedAtUtc,
    IReadOnlyList<MonitorRuleDto> Rules,
    IReadOnlyList<MonitorExecutionDto> RecentExecutions);

public sealed record CreateMonitorRuleRequest(
    RuleType RuleType,
    decimal ThresholdValue,
    ComparisonOperator ComparisonOperator,
    string? TimeWindow = "30d");

public sealed record CreateMonitorRequest(
    Guid DataSourceId,
    string Name,
    string? Description,
    MonitorType MonitorType,
    ScheduleType ScheduleType,
    string? QueryTemplate = null,
    string? TargetMetric = null,
    IReadOnlyList<CreateMonitorRuleRequest>? Rules = null);

public sealed record RunMonitorResponseDto(
    Guid ExecutionId,
    Guid MonitorId,
    string Status,
    decimal? CurrentMetricValue,
    decimal? BaselineMetricValue,
    string? Findings,
    bool AlertTriggered,
    long DurationMs);
