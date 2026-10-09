using IntelliQueryAI.Domain.Enums;
using IntelliQueryAI.Domain.Services;
using Microsoft.Extensions.Logging;

namespace IntelliQueryAI.Infrastructure.Services;

/// <summary>
/// Production statistical anomaly detection and trend analysis service.
/// Uses rolling window Z-Score statistics, percentage delta thresholds, and pattern recognition
/// to detect sudden revenue drops, activity gaps, spikes, and operational anomalies.
/// Designed with clear extension points for future ML/AI models.
/// </summary>
public sealed class StatisticalAnomalyDetectionService : IAnomalyDetectionService
{
    private readonly ILogger<StatisticalAnomalyDetectionService> _logger;

    public StatisticalAnomalyDetectionService(ILogger<StatisticalAnomalyDetectionService> logger)
    {
        _logger = logger;
    }

    public AnomalyDetectionResult DetectAnomaly(
        IReadOnlyList<MetricDataPoint> history,
        decimal currentValue,
        double zScoreThreshold = 2.5,
        decimal percentageThreshold = 20.0m)
    {
        var now = DateTime.UtcNow;

        if (history == null || history.Count == 0)
        {
            return new AnomalyDetectionResult(
                IsAnomaly: false,
                CurrentValue: currentValue,
                ExpectedValue: currentValue,
                DeviationPercentage: 0m,
                ZScore: 0.0,
                AlertType: AlertType.System,
                Severity: AlertSeverity.Information,
                Description: "Insufficient historical observations for statistical baseline calculation.",
                DetectedAtUtc: now);
        }

        // Calculate Mean (mu) and Standard Deviation (sigma)
        var values = history.Select(h => (double)h.Value).ToList();
        double mean = values.Average();
        double variance = values.Sum(v => Math.Pow(v - mean, 2)) / (values.Count > 1 ? values.Count - 1 : 1);
        double stdDev = Math.Sqrt(variance);

        double zScore = stdDev > 0.00001
            ? ((double)currentValue - mean) / stdDev
            : 0.0;

        decimal expectedValue = (decimal)mean;
        decimal deviationPercentage = expectedValue != 0
            ? ((currentValue - expectedValue) / Math.Abs(expectedValue)) * 100m
            : 0m;

        bool isZScoreAnomaly = Math.Abs(zScore) >= zScoreThreshold;
        bool isPercentAnomaly = Math.Abs(deviationPercentage) >= percentageThreshold;
        bool isAnomaly = isZScoreAnomaly || isPercentAnomaly;

        // Pattern Classification
        AlertType alertType = AlertType.AnomalyDetected;
        AlertSeverity severity = AlertSeverity.Information;
        string patternDescription;

        if (currentValue == 0 && expectedValue > 0)
        {
            // Customer or transaction inactivity
            alertType = AlertType.InactivityWarning;
            severity = AlertSeverity.Critical;
            patternDescription = $"Customer or metric inactivity detected: expected baseline of {expectedValue:N2}, but observed 0.00.";
        }
        else if (deviationPercentage <= -percentageThreshold || zScore <= -zScoreThreshold)
        {
            // Sudden drop
            alertType = AlertType.DropSpike;
            severity = Math.Abs(deviationPercentage) >= 40.0m || zScore <= -3.0
                ? AlertSeverity.Critical
                : AlertSeverity.Warning;

            patternDescription = $"Significant downward drop detected: current value ({currentValue:N2}) is {Math.Abs(deviationPercentage):F1}% below baseline ({expectedValue:N2}) with Z-score {zScore:F2}.";
        }
        else if (deviationPercentage >= percentageThreshold || zScore >= zScoreThreshold)
        {
            // Sudden surge / spike
            alertType = AlertType.DropSpike;
            severity = AlertSeverity.Warning;
            patternDescription = $"Significant upward spike detected: current value ({currentValue:N2}) exceeds baseline ({expectedValue:N2}) by {deviationPercentage:F1}% with Z-score {zScore:F2}.";
        }
        else
        {
            patternDescription = $"Value ({currentValue:N2}) is within standard operating parameters (Z-score: {zScore:F2}).";
        }

        _logger.LogDebug(
            "Anomaly evaluation completed. Value={Val}, Baseline={Base}, Z={Z}, IsAnomaly={Anomaly}",
            currentValue, expectedValue, zScore, isAnomaly);

        return new AnomalyDetectionResult(
            isAnomaly,
            currentValue,
            expectedValue,
            deviationPercentage,
            zScore,
            alertType,
            severity,
            patternDescription,
            now);
    }

    public IReadOnlyList<AnomalyDetectionResult> AnalyzeSeries(
        IReadOnlyList<MetricDataPoint> series,
        double zScoreThreshold = 2.5,
        decimal percentageThreshold = 20.0m)
    {
        if (series == null || series.Count < 3)
        {
            return Array.Empty<AnomalyDetectionResult>();
        }

        var anomalies = new List<AnomalyDetectionResult>();
        var window = new List<MetricDataPoint>();

        for (int i = 0; i < series.Count; i++)
        {
            var point = series[i];

            if (window.Count >= 3)
            {
                var result = DetectAnomaly(window, point.Value, zScoreThreshold, percentageThreshold);
                if (result.IsAnomaly)
                {
                    anomalies.Add(result);
                }
            }

            window.Add(point);
            if (window.Count > 30) // Rolling 30-observation window
            {
                window.RemoveAt(0);
            }
        }

        return anomalies;
    }

    public TrendAnalysisResult AnalyzeTrend(IReadOnlyList<MetricDataPoint> series)
    {
        if (series == null || series.Count == 0)
        {
            return new TrendAnalysisResult(
                Direction: "Stable",
                GrowthRatePercentage: 0m,
                AverageValue: 0m,
                MinValue: 0m,
                MaxValue: 0m,
                StandardDeviation: 0m,
                DataPoints: Array.Empty<MetricDataPoint>());
        }

        var ordered = series.OrderBy(p => p.TimestampUtc).ToList();
        var values = ordered.Select(p => p.Value).ToList();

        decimal avg = values.Average();
        decimal min = values.Min();
        decimal max = values.Max();

        double doubleAvg = (double)avg;
        double variance = values.Sum(v => Math.Pow((double)v - doubleAvg, 2)) / (values.Count > 1 ? values.Count - 1 : 1);
        decimal stdDev = (decimal)Math.Sqrt(variance);

        decimal first = values.First();
        decimal last = values.Last();
        decimal growthRate = first != 0
            ? ((last - first) / Math.Abs(first)) * 100m
            : 0m;

        string direction = growthRate switch
        {
            > 5.0m => "Upward",
            < -5.0m => "Downward",
            _ => "Stable"
        };

        return new TrendAnalysisResult(
            direction,
            growthRate,
            avg,
            min,
            max,
            stdDev,
            ordered);
    }
}
