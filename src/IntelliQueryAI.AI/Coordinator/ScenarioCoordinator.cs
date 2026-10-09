using System.Diagnostics;
using System.Text.Json;
using IntelliQueryAI.AI.Agents;
using IntelliQueryAI.AI.Telemetry;
using IntelliQueryAI.Domain.Entities.Scenarios;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.Domain.Services;
using IntelliQueryAI.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace IntelliQueryAI.AI.Coordinator;

public sealed record CalculateScenarioRequest(
    Guid WorkspaceId,
    string Title,
    string TargetMetric,
    decimal BaselineValue,
    string TimeHorizon,
    IReadOnlyList<ScenarioVariable> Variables,
    string? Description = null);

public sealed record CalculateScenarioResult(
    SimulationResult Simulation,
    ScenarioAnalysisNarrative Narrative,
    string SerializedNarrativeJson,
    long DurationMs);

public interface IScenarioCoordinator
{
    Task<Result<CalculateScenarioResult>> CalculateAndAnalyzeAsync(
        CalculateScenarioRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class ScenarioCoordinator : IScenarioCoordinator
{
    private readonly ScenarioAnalystAgent _agent;
    private readonly IScenarioSimulationEngine _simulationEngine;
    private readonly IMetricStore? _metricStore;
    private readonly IBusinessTermStore? _businessTermStore;
    private readonly ITelemetryService _telemetry;
    private readonly ILogger<ScenarioCoordinator> _logger;

    public ScenarioCoordinator(
        ScenarioAnalystAgent agent,
        IScenarioSimulationEngine simulationEngine,
        ITelemetryService telemetry,
        ILogger<ScenarioCoordinator> logger,
        IMetricStore? metricStore = null,
        IBusinessTermStore? businessTermStore = null)
    {
        _agent = agent;
        _simulationEngine = simulationEngine;
        _metricStore = metricStore;
        _businessTermStore = businessTermStore;
        _telemetry = telemetry;
        _logger = logger;
    }

    public async Task<Result<CalculateScenarioResult>> CalculateAndAnalyzeAsync(
        CalculateScenarioRequest request,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation("Executing Scenario Simulation for workspace '{WorkspaceId}', metric '{Metric}', baseline '{Baseline}'",
            request.WorkspaceId, request.TargetMetric, request.BaselineValue);

        // 1. Deterministic mathematical simulation (No hallucinated numbers!)
        var simulation = _simulationEngine.CalculateSimulation(
            request.BaselineValue,
            request.TimeHorizon,
            request.Variables);

        // 2. Retrieve Semantic Knowledge context if available (Phase A)
        string semanticContext = string.Empty;
        if (_metricStore != null || _businessTermStore != null)
        {
            var contextParts = new List<string>();

            if (_metricStore != null)
            {
                var matchingMetrics = await _metricStore.GetByWorkspaceIdAsync(request.WorkspaceId, isActive: true, cancellationToken: cancellationToken);
                var relevant = matchingMetrics.FirstOrDefault(m => m.Name.Contains(request.TargetMetric, StringComparison.OrdinalIgnoreCase));
                if (relevant != null)
                {
                    contextParts.Add($"Metric '{relevant.Name}': Formula={relevant.CalculationFormula}, Unit={relevant.Unit}");
                }
            }

            if (_businessTermStore != null)
            {
                var matchingTerms = await _businessTermStore.GetByWorkspaceIdAsync(request.WorkspaceId, isActive: true, cancellationToken: cancellationToken);
                var relevantTerm = matchingTerms.FirstOrDefault(t => t.Name.Contains(request.TargetMetric, StringComparison.OrdinalIgnoreCase));
                if (relevantTerm != null)
                {
                    contextParts.Add($"Term '{relevantTerm.Name}': {relevantTerm.Definition}");
                }
            }

            if (contextParts.Count > 0)
            {
                semanticContext = string.Join("\n", contextParts);
            }
        }

        // 3. Format variable context string
        var varDescriptions = request.Variables.Select(v =>
            $"• {v.Name}: Shift {v.ChangePercentage:+#0.00;-#0.00;0.00}% (Baseline: {v.BaselineValue} {v.Unit}, Weight: {v.Weight})");
        string variablesContext = string.Join("\n", varDescriptions);

        // 4. Autonomous Agent reasoning & strategic executive narrative synthesis
        var narrative = await _agent.AnalyzeScenarioAsync(
            targetMetric: request.TargetMetric,
            baselineValue: simulation.BaselineValue,
            expectedValue: simulation.ExpectedValue,
            bestCaseValue: simulation.BestCaseValue,
            worstCaseValue: simulation.WorstCaseValue,
            deltaPercentage: simulation.ExpectedDeltaPercentage,
            timeHorizon: request.TimeHorizon,
            variablesContext: variablesContext,
            semanticContext: semanticContext,
            cancellationToken: cancellationToken);

        stopwatch.Stop();

        string narrativeJson = JsonSerializer.Serialize(narrative, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        return Result.Success(new CalculateScenarioResult(
            Simulation: simulation,
            Narrative: narrative,
            SerializedNarrativeJson: narrativeJson,
            DurationMs: stopwatch.ElapsedMilliseconds));
    }
}
