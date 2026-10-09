using IntelliQueryAI.AI.Agents;
using IntelliQueryAI.Application.Features.Alerts.Dtos;
using IntelliQueryAI.Application.Features.ExecutiveIntelligence.Dtos;
using IntelliQueryAI.Application.Features.Monitoring.Dtos;

namespace IntelliQueryAI.Application.Features.Dashboard.Dtos;

public sealed record DashboardExecutiveSummaryResponseDto(
    Guid? ReportId,
    string ReportPeriod,
    string Title,
    string Summary,
    IReadOnlyList<string> KeyFindings,
    IReadOnlyList<string> Risks,
    IReadOnlyList<string> Opportunities,
    IReadOnlyList<ActionableRecommendation> Recommendations,
    DateTime? GeneratedAtUtc);

public sealed record DashboardAlertsResponseDto(
    int TotalOpenAlerts,
    int CriticalCount,
    int WarningCount,
    int InformationCount,
    IReadOnlyList<AlertDto> RecentAlerts);

public sealed record DashboardMonitoringResponseDto(
    int TotalMonitors,
    int ActiveMonitors,
    int ExecutionsLast24Hours,
    int HealthyMonitors,
    double SystemHealthPercentage,
    IReadOnlyList<MonitorDto> Monitors,
    IReadOnlyList<MonitorExecutionDto> RecentExecutions);

public sealed record TrendPointDto(
    DateTime TimestampUtc,
    string Label,
    decimal Value);

public sealed record DashboardTrendsResponseDto(
    string Direction,
    decimal GrowthRatePercentage,
    decimal AverageDailyQueries,
    decimal TotalQueriesPeriod,
    IReadOnlyList<TrendPointDto> DataPoints);

public sealed record AnomalyPointDto(
    DateTime DetectedAtUtc,
    string AnomalyType,
    string Severity,
    decimal CurrentValue,
    decimal ExpectedValue,
    decimal DeviationPercentage,
    double ZScore,
    string Description);

public sealed record DashboardAnomaliesResponseDto(
    int TotalAnomaliesDetected,
    IReadOnlyList<AnomalyPointDto> Anomalies);
