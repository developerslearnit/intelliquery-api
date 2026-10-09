using IntelliQueryAI.AI.Coordinator;
using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Application.Features.Forecasting.Dtos;
using IntelliQueryAI.Domain.Entities.Forecasting;
using IntelliQueryAI.Domain.Errors;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.SharedKernel.Results;

namespace IntelliQueryAI.Application.Features.Forecasting.Queries;

public sealed record GetForecastsQuery(
    Guid WorkspaceId,
    string? MetricName = null,
    int Limit = 50) : IQuery<Result<IReadOnlyList<ForecastDto>>>;

public sealed class GetForecastsQueryHandler
    : IQueryHandler<GetForecastsQuery, Result<IReadOnlyList<ForecastDto>>>
{
    private readonly IForecastStore _store;

    public GetForecastsQueryHandler(IForecastStore store)
    {
        _store = store;
    }

    public async Task<Result<IReadOnlyList<ForecastDto>>> HandleAsync(
        GetForecastsQuery query,
        CancellationToken cancellationToken = default)
    {
        var entities = await _store.GetByWorkspaceAsync(
            query.WorkspaceId,
            query.MetricName,
            query.Limit,
            cancellationToken);

        var dtos = entities.Select(ForecastMapping.ToDto).ToList();
        return Result.Success<IReadOnlyList<ForecastDto>>(dtos);
    }
}

public sealed record GetForecastByIdQuery(
    Guid WorkspaceId,
    Guid ForecastId) : IQuery<Result<ForecastDto>>;

public sealed class GetForecastByIdQueryHandler
    : IQueryHandler<GetForecastByIdQuery, Result<ForecastDto>>
{
    private readonly IForecastStore _store;

    public GetForecastByIdQueryHandler(IForecastStore store)
    {
        _store = store;
    }

    public async Task<Result<ForecastDto>> HandleAsync(
        GetForecastByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        var forecast = await _store.GetByIdAsync(query.ForecastId, query.WorkspaceId, cancellationToken);
        if (forecast == null)
        {
            return Result.Failure<ForecastDto>(AiErrors.ForecastNotFound);
        }

        return Result.Success(ForecastMapping.ToDto(forecast));
    }
}

public sealed record QuickForecastPreviewQuery(
    Guid WorkspaceId,
    QuickForecastPreviewRequestDto Request) : IQuery<Result<QuickForecastPreviewDto>>;

public sealed class QuickForecastPreviewQueryHandler
    : IQueryHandler<QuickForecastPreviewQuery, Result<QuickForecastPreviewDto>>
{
    private readonly IForecastCoordinator _coordinator;

    public QuickForecastPreviewQueryHandler(IForecastCoordinator coordinator)
    {
        _coordinator = coordinator;
    }

    public async Task<Result<QuickForecastPreviewDto>> HandleAsync(
        QuickForecastPreviewQuery query,
        CancellationToken cancellationToken = default)
    {
        var req = query.Request;

        if (string.IsNullOrWhiteSpace(req.MetricName))
        {
            return Result.Failure<QuickForecastPreviewDto>(Error.Validation("Forecast.EmptyMetricName", "Metric name is required."));
        }

        if (req.HistoricalPoints == null || req.HistoricalPoints.Count < 2)
        {
            return Result.Failure<QuickForecastPreviewDto>(AiErrors.InsufficientHistoricalData("At least 2 historical observations are required."));
        }

        if (!Enum.TryParse<ForecastMethod>(req.Method, true, out var method))
        {
            method = ForecastMethod.LinearTrend;
        }

        var domainHistoricalPoints = req.HistoricalPoints
            .Select(p => new HistoricalDataPoint(p.Period, p.Value))
            .ToList();

        var (engineOutput, aiExplanation) = await _coordinator.PreviewForecastAsync(
            query.WorkspaceId,
            req.MetricName.Trim(),
            req.HistoricalPeriod ?? "Past Period",
            req.ForecastPeriod ?? "Next Period",
            method,
            req.HorizonSteps > 0 ? req.HorizonSteps : 3,
            req.ConfidenceLevel is > 0 and < 1 ? req.ConfidenceLevel : 0.95,
            domainHistoricalPoints,
            cancellationToken);

        var preds = engineOutput.PredictionPoints
            .Select(p => new ForecastPredictionPointDto(p.Period, p.ForecastValue, p.LowerBound, p.UpperBound))
            .ToList();

        var assumptions = engineOutput.Assumptions
            .Select(a => new ForecastAssumptionDto(a.Key, a.Value, a.Description))
            .ToList();

        var dto = new QuickForecastPreviewDto(
            req.MetricName.Trim(),
            method.ToString(),
            preds,
            assumptions,
            engineOutput.ProjectedGrowthRate,
            engineOutput.FinalProjectedValue,
            engineOutput.MeanAbsoluteError,
            engineOutput.RSquared,
            aiExplanation);

        return Result.Success(dto);
    }
}
