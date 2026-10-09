using System.Text.Json;
using IntelliQueryAI.SharedKernel.Primitives;

namespace IntelliQueryAI.Domain.Entities.Scenarios;

public sealed record ScenarioVariable(
    string Name,
    string Unit,
    decimal BaselineValue,
    decimal ChangePercentage,
    decimal? MinBound = null,
    decimal? MaxBound = null,
    double Weight = 1.0);

public sealed record MonthlyDataPoint(
    string Month,
    decimal Baseline,
    decimal Projected,
    decimal? BestCase = null,
    decimal? WorstCase = null);

public sealed record ScenarioProjection(
    string CaseType, // "Baseline", "Expected", "BestCase", "WorstCase"
    decimal ProjectedValue,
    decimal DeltaValue,
    decimal DeltaPercentage,
    IReadOnlyList<MonthlyDataPoint>? MonthlyBreakdown = null);

public sealed class Scenario : AggregateRoot
{
    public Guid WorkspaceId { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string TargetMetric { get; private set; } = string.Empty;
    public decimal BaselineValue { get; private set; }
    public string TimeHorizon { get; private set; } = "Next 12 Months"; // "Next Quarter", "Next 6 Months", "Next 12 Months"
    public string VariablesJson { get; private set; } = "[]";
    public string ProjectionsJson { get; private set; } = "[]";
    public string? AiExplanation { get; private set; }
    public double ConfidenceScore { get; private set; } = 0.85;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    private Scenario(
        Guid workspaceId,
        Guid createdByUserId,
        string title,
        string? description,
        string targetMetric,
        decimal baselineValue,
        string timeHorizon,
        string variablesJson,
        string projectionsJson,
        string? aiExplanation,
        double confidenceScore)
        : base(Guid.NewGuid())
    {
        WorkspaceId = workspaceId;
        CreatedByUserId = createdByUserId;
        Title = title.Trim();
        Description = description?.Trim();
        TargetMetric = targetMetric.Trim();
        BaselineValue = baselineValue;
        TimeHorizon = string.IsNullOrWhiteSpace(timeHorizon) ? "Next 12 Months" : timeHorizon.Trim();
        VariablesJson = variablesJson;
        ProjectionsJson = projectionsJson;
        AiExplanation = aiExplanation;
        ConfidenceScore = Math.Clamp(confidenceScore, 0.1, 1.0);
        CreatedAtUtc = DateTime.UtcNow;
    }

    private Scenario() { }

    public static Scenario Create(
        Guid workspaceId,
        Guid createdByUserId,
        string title,
        string? description,
        string targetMetric,
        decimal baselineValue,
        string timeHorizon,
        IReadOnlyList<ScenarioVariable> variables,
        IReadOnlyList<ScenarioProjection> projections,
        string? aiExplanation = null,
        double confidenceScore = 0.85)
    {
        return new Scenario(
            workspaceId,
            createdByUserId,
            title,
            description,
            targetMetric,
            baselineValue,
            timeHorizon,
            JsonSerializer.Serialize(variables),
            JsonSerializer.Serialize(projections),
            aiExplanation,
            confidenceScore);
    }

    public void UpdateSimulationResults(
        IReadOnlyList<ScenarioVariable> variables,
        IReadOnlyList<ScenarioProjection> projections,
        string? aiExplanation,
        double confidenceScore)
    {
        VariablesJson = JsonSerializer.Serialize(variables);
        ProjectionsJson = JsonSerializer.Serialize(projections);
        AiExplanation = aiExplanation;
        ConfidenceScore = Math.Clamp(confidenceScore, 0.1, 1.0);
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void UpdateDetails(string title, string? description)
    {
        Title = title.Trim();
        Description = description?.Trim();
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public IReadOnlyList<ScenarioVariable> GetVariables()
    {
        if (string.IsNullOrWhiteSpace(VariablesJson)) return Array.Empty<ScenarioVariable>();
        try
        {
            return JsonSerializer.Deserialize<List<ScenarioVariable>>(VariablesJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                   ?? (IReadOnlyList<ScenarioVariable>)Array.Empty<ScenarioVariable>();
        }
        catch
        {
            return Array.Empty<ScenarioVariable>();
        }
    }

    public IReadOnlyList<ScenarioProjection> GetProjections()
    {
        if (string.IsNullOrWhiteSpace(ProjectionsJson)) return Array.Empty<ScenarioProjection>();
        try
        {
            return JsonSerializer.Deserialize<List<ScenarioProjection>>(ProjectionsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                   ?? (IReadOnlyList<ScenarioProjection>)Array.Empty<ScenarioProjection>();
        }
        catch
        {
            return Array.Empty<ScenarioProjection>();
        }
    }
}
