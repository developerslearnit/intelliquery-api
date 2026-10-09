using IntelliQueryAI.Domain.Entities.Forecasting;

namespace IntelliQueryAI.Application.Features.Forecasting.Dtos;

public sealed record HistoricalDataPointDto(
    string Period,
    decimal Value);

public sealed record ForecastPredictionPointDto(
    string Period,
    decimal ForecastValue,
    decimal LowerBound,
    decimal UpperBound);

public sealed record ForecastAssumptionDto(
    string Key,
    string Value,
    string Description);

public sealed record ForecastDto(
    Guid Id,
    Guid WorkspaceId,
    Guid? CreatedByUserId,
    Guid? DataSourceId,
    string MetricName,
    string ForecastPeriod,
    string HistoricalPeriod,
    string Method,
    double ConfidenceInterval,
    IReadOnlyList<HistoricalDataPointDto> HistoricalPoints,
    IReadOnlyList<ForecastPredictionPointDto> ForecastPoints,
    IReadOnlyList<ForecastAssumptionDto> Assumptions,
    string? AiExplanation,
    decimal ProjectedGrowthRate,
    decimal FinalProjectedValue,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed record GenerateForecastRequestDto(
    string MetricName,
    string HistoricalPeriod,
    string ForecastPeriod,
    string Method,
    int HorizonSteps = 3,
    double ConfidenceLevel = 0.95,
    IReadOnlyList<HistoricalDataPointDto>? HistoricalPoints = null,
    Guid? DataSourceId = null);

public sealed record QuickForecastPreviewRequestDto(
    string MetricName,
    string HistoricalPeriod,
    string ForecastPeriod,
    string Method,
    int HorizonSteps = 3,
    double ConfidenceLevel = 0.95,
    IReadOnlyList<HistoricalDataPointDto>? HistoricalPoints = null);

public sealed record QuickForecastPreviewDto(
    string MetricName,
    string Method,
    IReadOnlyList<ForecastPredictionPointDto> ForecastPoints,
    IReadOnlyList<ForecastAssumptionDto> Assumptions,
    decimal ProjectedGrowthRate,
    decimal FinalProjectedValue,
    double MeanAbsoluteError,
    double RSquared,
    string AiExplanation);

public static class ForecastMapping
{
    public static ForecastDto ToDto(Forecast entity)
    {
        var hist = entity.GetHistoricalPoints()
            .Select(p => new HistoricalDataPointDto(p.Period, p.Value))
            .ToList();

        var preds = entity.GetPredictionPoints()
            .Select(p => new ForecastPredictionPointDto(p.Period, p.ForecastValue, p.LowerBound, p.UpperBound))
            .ToList();

        var assumptions = entity.GetAssumptions()
            .Select(a => new ForecastAssumptionDto(a.Key, a.Value, a.Description))
            .ToList();

        return new ForecastDto(
            entity.Id,
            entity.WorkspaceId,
            entity.CreatedByUserId,
            entity.DataSourceId,
            entity.MetricName,
            entity.ForecastPeriod,
            entity.HistoricalPeriod,
            entity.Method.ToString(),
            entity.ConfidenceInterval,
            hist,
            preds,
            assumptions,
            entity.AiExplanation,
            entity.ProjectedGrowthRate,
            entity.FinalProjectedValue,
            entity.CreatedAtUtc,
            entity.UpdatedAtUtc);
    }
}
