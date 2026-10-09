using IntelliQueryAI.AI.Coordinator;
using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Application.Features.Forecasting.Dtos;
using IntelliQueryAI.Domain.Entities.Forecasting;
using IntelliQueryAI.Domain.Errors;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.SharedKernel.Results;

namespace IntelliQueryAI.Application.Features.Forecasting.Commands;

public sealed record GenerateForecastCommand(
    Guid WorkspaceId,
    Guid? UserId,
    GenerateForecastRequestDto Request) : ICommand<Result<ForecastDto>>;

public sealed class GenerateForecastCommandHandler
    : ICommandHandler<GenerateForecastCommand, Result<ForecastDto>>
{
    private readonly IForecastCoordinator _coordinator;

    public GenerateForecastCommandHandler(IForecastCoordinator coordinator)
    {
        _coordinator = coordinator;
    }

    public async Task<Result<ForecastDto>> HandleAsync(
        GenerateForecastCommand command,
        CancellationToken cancellationToken = default)
    {
        var req = command.Request;

        if (string.IsNullOrWhiteSpace(req.MetricName))
        {
            return Result.Failure<ForecastDto>(Error.Validation("Forecast.EmptyMetricName", "Metric name is required."));
        }

        if (req.HistoricalPoints == null || req.HistoricalPoints.Count < 2)
        {
            return Result.Failure<ForecastDto>(AiErrors.InsufficientHistoricalData("At least 2 historical observations are required."));
        }

        if (!Enum.TryParse<ForecastMethod>(req.Method, true, out var method))
        {
            method = ForecastMethod.LinearTrend;
        }

        var domainHistoricalPoints = req.HistoricalPoints
            .Select(p => new HistoricalDataPoint(p.Period, p.Value))
            .ToList();

        var forecast = await _coordinator.GenerateAndSaveForecastAsync(
            workspaceId: command.WorkspaceId,
            userId: command.UserId,
            dataSourceId: req.DataSourceId,
            metricName: req.MetricName.Trim(),
            historicalPeriod: req.HistoricalPeriod ?? "Past Period",
            forecastPeriod: req.ForecastPeriod ?? "Next Period",
            method: method,
            horizonSteps: req.HorizonSteps > 0 ? req.HorizonSteps : 3,
            confidenceLevel: req.ConfidenceLevel is > 0 and < 1 ? req.ConfidenceLevel : 0.95,
            historicalPoints: domainHistoricalPoints,
            cancellationToken: cancellationToken);

        return Result.Success(ForecastMapping.ToDto(forecast));
    }
}

public sealed record DeleteForecastCommand(
    Guid WorkspaceId,
    Guid ForecastId) : ICommand<Result>;

public sealed class DeleteForecastCommandHandler
    : ICommandHandler<DeleteForecastCommand, Result>
{
    private readonly IForecastStore _store;

    public DeleteForecastCommandHandler(IForecastStore store)
    {
        _store = store;
    }

    public async Task<Result> HandleAsync(
        DeleteForecastCommand command,
        CancellationToken cancellationToken = default)
    {
        var forecast = await _store.GetByIdAsync(command.ForecastId, command.WorkspaceId, cancellationToken);
        if (forecast == null)
        {
            return Result.Failure(AiErrors.ForecastNotFound);
        }

        await _store.DeleteAsync(forecast, cancellationToken);
        return Result.Success();
    }
}
