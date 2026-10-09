using System.Text.Json;
using IntelliQueryAI.SharedKernel.Primitives;

namespace IntelliQueryAI.Domain.Entities.Forecasting;

public enum ForecastMethod
{
    LinearTrend = 0,
    MovingAverage = 1,
    ExponentialSmoothing = 2,
    SeasonalGrowth = 3
}

public sealed record HistoricalDataPoint(
    string Period,
    decimal Value);

public sealed record ForecastPredictionPoint(
    string Period,
    decimal ForecastValue,
    decimal LowerBound,
    decimal UpperBound);

public sealed record ForecastAssumption(
    string Key,
    string Value,
    string Description);

/// <summary>
/// Aggregate root representing an evidence-backed statistical forecast.
/// Combines deterministic statistical modeling (moving averages, trend extrapolation, confidence intervals)
/// with AI narrative explanation and seamless What-If scenario integration.
/// </summary>
public sealed class Forecast : AggregateRoot
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public Guid WorkspaceId { get; private set; }
    public Guid? CreatedByUserId { get; private set; }
    public Guid? DataSourceId { get; private set; }
    public string MetricName { get; private set; } = string.Empty;
    public string ForecastPeriod { get; private set; } = string.Empty;
    public string HistoricalPeriod { get; private set; } = string.Empty;
    public ForecastMethod Method { get; private set; }
    public double ConfidenceInterval { get; private set; } = 0.95;
    public string HistoricalDataPointsJson { get; private set; } = "[]";
    public string ForecastDataPointsJson { get; private set; } = "[]";
    public string AssumptionsJson { get; private set; } = "[]";
    public string? AiExplanation { get; private set; }
    public decimal ProjectedGrowthRate { get; private set; }
    public decimal FinalProjectedValue { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    private Forecast() { }

    public Forecast(
        Guid workspaceId,
        Guid? createdByUserId,
        Guid? dataSourceId,
        string metricName,
        string forecastPeriod,
        string historicalPeriod,
        ForecastMethod method,
        double confidenceInterval,
        IReadOnlyList<HistoricalDataPoint> historicalPoints,
        IReadOnlyList<ForecastPredictionPoint> forecastPoints,
        IReadOnlyList<ForecastAssumption> assumptions,
        decimal projectedGrowthRate,
        decimal finalProjectedValue,
        string? aiExplanation = null)
    {
        Id = Guid.NewGuid();
        WorkspaceId = workspaceId;
        CreatedByUserId = createdByUserId;
        DataSourceId = dataSourceId;
        MetricName = metricName;
        ForecastPeriod = forecastPeriod;
        HistoricalPeriod = historicalPeriod;
        Method = method;
        ConfidenceInterval = confidenceInterval;
        HistoricalDataPointsJson = JsonSerializer.Serialize(historicalPoints, JsonOptions);
        ForecastDataPointsJson = JsonSerializer.Serialize(forecastPoints, JsonOptions);
        AssumptionsJson = JsonSerializer.Serialize(assumptions, JsonOptions);
        ProjectedGrowthRate = projectedGrowthRate;
        FinalProjectedValue = finalProjectedValue;
        AiExplanation = aiExplanation;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public IReadOnlyList<HistoricalDataPoint> GetHistoricalPoints()
    {
        if (string.IsNullOrWhiteSpace(HistoricalDataPointsJson))
            return Array.Empty<HistoricalDataPoint>();

        try
        {
            return JsonSerializer.Deserialize<IReadOnlyList<HistoricalDataPoint>>(HistoricalDataPointsJson, JsonOptions)
                   ?? Array.Empty<HistoricalDataPoint>();
        }
        catch
        {
            return Array.Empty<HistoricalDataPoint>();
        }
    }

    public IReadOnlyList<ForecastPredictionPoint> GetPredictionPoints()
    {
        if (string.IsNullOrWhiteSpace(ForecastDataPointsJson))
            return Array.Empty<ForecastPredictionPoint>();

        try
        {
            return JsonSerializer.Deserialize<IReadOnlyList<ForecastPredictionPoint>>(ForecastDataPointsJson, JsonOptions)
                   ?? Array.Empty<ForecastPredictionPoint>();
        }
        catch
        {
            return Array.Empty<ForecastPredictionPoint>();
        }
    }

    public IReadOnlyList<ForecastAssumption> GetAssumptions()
    {
        if (string.IsNullOrWhiteSpace(AssumptionsJson))
            return Array.Empty<ForecastAssumption>();

        try
        {
            return JsonSerializer.Deserialize<IReadOnlyList<ForecastAssumption>>(AssumptionsJson, JsonOptions)
                   ?? Array.Empty<ForecastAssumption>();
        }
        catch
        {
            return Array.Empty<ForecastAssumption>();
        }
    }

    public void UpdateAiExplanation(string explanation)
    {
        AiExplanation = explanation;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
