using IntelliQueryAI.AI.Agents;
using IntelliQueryAI.Domain.Entities.Forecasting;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.Domain.Services;
using Microsoft.Extensions.Logging;

namespace IntelliQueryAI.AI.Coordinator;

public interface IForecastCoordinator
{
    Task<Forecast> GenerateAndSaveForecastAsync(
        Guid workspaceId,
        Guid? userId,
        Guid? dataSourceId,
        string metricName,
        string historicalPeriod,
        string forecastPeriod,
        ForecastMethod method,
        int horizonSteps,
        double confidenceLevel,
        IReadOnlyList<HistoricalDataPoint> historicalPoints,
        CancellationToken cancellationToken = default);

    Task<(ForecastEngineOutput EngineOutput, string AiExplanation)> PreviewForecastAsync(
        Guid workspaceId,
        string metricName,
        string historicalPeriod,
        string forecastPeriod,
        ForecastMethod method,
        int horizonSteps,
        double confidenceLevel,
        IReadOnlyList<HistoricalDataPoint> historicalPoints,
        CancellationToken cancellationToken = default);
}

public sealed class ForecastCoordinator : IForecastCoordinator
{
    private readonly IForecastingEngine _forecastingEngine;
    private readonly ForecastAnalystAgent _forecastAnalystAgent;
    private readonly IForecastStore _forecastStore;
    private readonly ILogger<ForecastCoordinator> _logger;

    public ForecastCoordinator(
        IForecastingEngine forecastingEngine,
        ForecastAnalystAgent forecastAnalystAgent,
        IForecastStore forecastStore,
        ILogger<ForecastCoordinator> logger)
    {
        _forecastingEngine = forecastingEngine;
        _forecastAnalystAgent = forecastAnalystAgent;
        _forecastStore = forecastStore;
        _logger = logger;
    }

    public async Task<Forecast> GenerateAndSaveForecastAsync(
        Guid workspaceId,
        Guid? userId,
        Guid? dataSourceId,
        string metricName,
        string historicalPeriod,
        string forecastPeriod,
        ForecastMethod method,
        int horizonSteps,
        double confidenceLevel,
        IReadOnlyList<HistoricalDataPoint> historicalPoints,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Coordinating forecast generation for workspace {WorkspaceId}, metric '{MetricName}', method {Method}",
            workspaceId, metricName, method);

        // 1. Run deterministic forecasting engine
        var engineOutput = _forecastingEngine.ComputeForecast(
            historicalPoints,
            method,
            horizonSteps,
            confidenceLevel);

        // 2. Synthesize AI narrative explanation via MAF ForecastAnalystAgent
        var aiExplanation = await _forecastAnalystAgent.GenerateExplanationAsync(
            metricName,
            historicalPeriod,
            forecastPeriod,
            method,
            confidenceLevel,
            historicalPoints,
            engineOutput,
            cancellationToken);

        // 3. Construct Aggregate Root
        var forecast = new Forecast(
            workspaceId: workspaceId,
            createdByUserId: userId,
            dataSourceId: dataSourceId,
            metricName: metricName,
            forecastPeriod: forecastPeriod,
            historicalPeriod: historicalPeriod,
            method: method,
            confidenceInterval: confidenceLevel,
            historicalPoints: historicalPoints,
            forecastPoints: engineOutput.PredictionPoints,
            assumptions: engineOutput.Assumptions,
            projectedGrowthRate: engineOutput.ProjectedGrowthRate,
            finalProjectedValue: engineOutput.FinalProjectedValue,
            aiExplanation: aiExplanation);

        // 4. Persist to EF Core store
        await _forecastStore.AddAsync(forecast, cancellationToken);
        _logger.LogInformation("Successfully persisted forecast {ForecastId} for workspace {WorkspaceId}", forecast.Id, workspaceId);

        return forecast;
    }

    public async Task<(ForecastEngineOutput EngineOutput, string AiExplanation)> PreviewForecastAsync(
        Guid workspaceId,
        string metricName,
        string historicalPeriod,
        string forecastPeriod,
        ForecastMethod method,
        int horizonSteps,
        double confidenceLevel,
        IReadOnlyList<HistoricalDataPoint> historicalPoints,
        CancellationToken cancellationToken = default)
    {
        var engineOutput = _forecastingEngine.ComputeForecast(
            historicalPoints,
            method,
            horizonSteps,
            confidenceLevel);

        var aiExplanation = await _forecastAnalystAgent.GenerateExplanationAsync(
            metricName,
            historicalPeriod,
            forecastPeriod,
            method,
            confidenceLevel,
            historicalPoints,
            engineOutput,
            cancellationToken);

        return (engineOutput, aiExplanation);
    }
}
