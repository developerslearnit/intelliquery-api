using IntelliQueryAI.Domain.Enums;

namespace IntelliQueryAI.Domain.Services;

/// <summary>
/// A time-series metric observation used for statistical anomaly detection and trend analysis.
/// </summary>
public sealed record MetricDataPoint(
    DateTime TimestampUtc,
    decimal Value,
    string? Label = null,
    string? Dimension = null);

/// <summary>
/// Outcome of statistical or algorithmic anomaly evaluation.
/// </summary>
public sealed record AnomalyDetectionResult(
    bool IsAnomaly,
    decimal CurrentValue,
    decimal ExpectedValue,
    decimal DeviationPercentage,
    double ZScore,
    AlertType AlertType,
    AlertSeverity Severity,
    string Description,
    DateTime DetectedAtUtc,
    IReadOnlyDictionary<string, object>? Metadata = null);

/// <summary>
/// Trend direction and trajectory metrics.
/// </summary>
public sealed record TrendAnalysisResult(
    string Direction, // "Upward", "Downward", "Stable"
    decimal GrowthRatePercentage,
    decimal AverageValue,
    decimal MinValue,
    decimal MaxValue,
    decimal StandardDeviation,
    IReadOnlyList<MetricDataPoint> DataPoints);

/// <summary>
/// Domain service interface for time-series trend analysis, statistical anomaly detection, and pattern recognition.
/// Serves as the primary extension point for statistical algorithms and future ML/AI models.
/// </summary>
public interface IAnomalyDetectionService
{
    /// <summary>
    /// Detects statistical anomalies using rolling Z-Score and percentage change thresholds.
    /// </summary>
    AnomalyDetectionResult DetectAnomaly(
        IReadOnlyList<MetricDataPoint> history,
        decimal currentValue,
        double zScoreThreshold = 2.5,
        decimal percentageThreshold = 20.0m);

    /// <summary>
    /// Identifies sudden revenue drops, spikes, customer inactivity, or unusual variances in historical series.
    /// </summary>
    IReadOnlyList<AnomalyDetectionResult> AnalyzeSeries(
        IReadOnlyList<MetricDataPoint> series,
        double zScoreThreshold = 2.5,
        decimal percentageThreshold = 20.0m);

    /// <summary>
    /// Computes statistical trend indicators including growth rate, mean, and standard deviation.
    /// </summary>
    TrendAnalysisResult AnalyzeTrend(IReadOnlyList<MetricDataPoint> series);
}
