using IntelliQueryAI.AI.Agents;

namespace IntelliQueryAI.Application.Features.Dashboard.Dtos;

public sealed record MorningExecutiveBriefDto(
    Guid? ReportId,
    string Greeting,
    string Headline,
    string Period,
    string Summary,
    DateTime GeneratedAtUtc,
    IReadOnlyList<ExecutiveBriefMetricDto> Metrics,
    IReadOnlyList<ExecutiveBriefAlertDto> CriticalAlerts,
    IReadOnlyList<ExecutiveBriefAnomalyDto> MajorAnomalies,
    IReadOnlyList<ExecutiveBriefFindingDto> RankedFindings,
    IReadOnlyList<ExecutiveBriefRiskDto> EmergingRisks,
    IReadOnlyList<ExecutiveBriefOpportunityDto> Opportunities,
    IReadOnlyList<ActionableRecommendation> Recommendations);

public sealed record ExecutiveBriefMetricDto(
    string Name,
    decimal CurrentValue,
    string FormattedValue,
    decimal? BaselineValue,
    decimal? ChangePercentage,
    string ChangeDirection, // "Up", "Down", "Neutral"
    string Status,          // "Healthy", "Warning", "Critical"
    string Priority,        // "High", "Medium", "Low"
    string Category,        // "Revenue", "Customers", "Operations", "Performance"
    Guid? MetricId = null,
    Guid? MonitorId = null,
    string? Explanation = null,
    string? Unit = null);

public sealed record ExecutiveBriefAlertDto(
    Guid Id,
    string Title,
    string Severity,
    string MonitorName,
    DateTime TriggeredAtUtc,
    decimal? MetricValue,
    decimal? ThresholdValue,
    string Description);

public sealed record ExecutiveBriefAnomalyDto(
    Guid Id,
    string MetricName,
    string Severity,
    decimal CurrentValue,
    decimal ExpectedValue,
    decimal DeviationPercentage,
    double ZScore,
    DateTime DetectedAtUtc,
    string Description);

public sealed record ExecutiveBriefFindingDto(
    int Rank,
    string Title,
    string Impact, // "High", "Medium", "Low"
    string Severity,
    double Confidence,
    string? Category = null);

public sealed record ExecutiveBriefRiskDto(
    int Rank,
    string Title,
    string Severity, // "Critical", "High", "Medium"
    string Impact);

public sealed record ExecutiveBriefOpportunityDto(
    int Rank,
    string Title,
    string PotentialImpact,
    string? SuggestedAction = null);

public sealed record GenerateMorningBriefRequestDto(
    string? Period = null);
