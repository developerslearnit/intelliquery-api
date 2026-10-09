using System.Text.Json;
using System.Text.Json.Serialization;
using IntelliQueryAI.AI.Common;
using IntelliQueryAI.AI.Telemetry;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IntelliQueryAI.AI.Agents;

public sealed record ScenarioAnalysisNarrative(
    [property: JsonPropertyName("summary")] string Summary,
    [property: JsonPropertyName("keyDrivers")] IReadOnlyList<string> KeyDrivers,
    [property: JsonPropertyName("riskFactors")] IReadOnlyList<string> RiskFactors,
    [property: JsonPropertyName("upsideCatalysts")] IReadOnlyList<string> UpsideCatalysts,
    [property: JsonPropertyName("strategicRecommendations")] IReadOnlyList<string> StrategicRecommendations);

/// <summary>
/// Autonomous AI Scenario Analyst Agent.
/// Synthesizes executive strategic explanations, sensitivity trade-offs, and risk mitigations
/// for deterministic What-If simulation projections.
/// Invariant: NEVER fabricates raw numbers. Explains mathematically calculated outcomes.
/// </summary>
public sealed class ScenarioAnalystAgent : BaseAgent
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ILogger<ScenarioAnalystAgent> _logger;

    public ScenarioAnalystAgent(
        IChatClient? chatClient,
        ITelemetryService telemetry,
        ILogger<ScenarioAnalystAgent> logger,
        IOptions<AzureOpenAISettings> options,
        SimulationMode simulationMode)
        : base(
            chatClient,
            instructions: """
                You are the Principal Scenario & What-If Intelligence Analyst Agent for IntelliQueryAI.
                Your role is to explain mathematically projected business outcomes based on hypothetical assumptions and variable shifts.

                CRITICAL ANALYTICAL & GOVERNANCE INVARIANTS:
                - NEVER fabricate raw numbers or speculate wildly. You are provided with deterministic baseline, expected, best-case, and worst-case projections.
                - Clearly identify the mathematical sensitivity drivers, assumptions, trade-offs, and external risk factors.
                - Output ONLY valid JSON adhering strictly to the ScenarioAnalysisNarrative structure:
                  {
                    "summary": "Executive summary of what the scenario projections indicate for the business",
                    "keyDrivers": ["Primary variable driver 1", "Primary variable driver 2"],
                    "riskFactors": ["Sensitivity risk 1", "Downside risk 2"],
                    "upsideCatalysts": ["Upside catalyst 1", "Expansion opportunity 2"],
                    "strategicRecommendations": ["Actionable mitigation step 1", "Capital allocation decision 2"]
                  }
                - Do not include markdown code fences or conversational boilerplate in the response.
                """,
            name: "ScenarioAnalystAgent",
            description: "Synthesizes executive strategic explanations for What-If scenario projections.",
            tools: null,
            telemetry: telemetry,
            logger: logger,
            simulationMode: simulationMode,
            temperature: options?.Value?.Temperature ?? 0.2f)
    {
        _logger = logger;
    }

    public async Task<ScenarioAnalysisNarrative> AnalyzeScenarioAsync(
        string targetMetric,
        decimal baselineValue,
        decimal expectedValue,
        decimal bestCaseValue,
        decimal worstCaseValue,
        decimal deltaPercentage,
        string timeHorizon,
        string variablesContext,
        string? semanticContext = null,
        CancellationToken cancellationToken = default)
    {
        string prompt = $"""
            Analyze the following What-If Scenario simulation:
            Target Metric: {targetMetric}
            Time Horizon: {timeHorizon}
            Baseline Starting Value: {baselineValue:N2}
            Projected Expected Value: {expectedValue:N2} (Net Delta: {deltaPercentage:+#0.00;-#0.00;0.00}%)
            Optimistic Best-Case Value: {bestCaseValue:N2}
            Stress-Test Worst-Case Value: {worstCaseValue:N2}

            Scenario Assumptions & Variable Levers:
            {variablesContext}

            {(!string.IsNullOrWhiteSpace(semanticContext) ? $"Organizational Semantic Context:\n{semanticContext}\n" : "")}

            Synthesize a rigorous executive narrative in valid JSON adhering to the ScenarioAnalysisNarrative schema.
            """;

        try
        {
            var response = await ExecuteAsync(new AgentRequest(prompt), cancellationToken);
            string clean = StripMarkdownFences(response.Content);
            var parsed = JsonSerializer.Deserialize<ScenarioAnalysisNarrative>(clean, JsonOptions);

            return parsed ?? GenerateSimulatedNarrative(targetMetric, deltaPercentage, timeHorizon);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse scenario narrative from LLM; using resilient fallback.");
            return GenerateSimulatedNarrative(targetMetric, deltaPercentage, timeHorizon);
        }
    }

    protected override string GenerateSimulatedResponse(AgentRequest request)
    {
        var narrative = GenerateSimulatedNarrative("Target Metric", 8.0m, "Next 12 Months");
        return JsonSerializer.Serialize(narrative, JsonOptions);
    }

    private static ScenarioAnalysisNarrative GenerateSimulatedNarrative(
        string targetMetric,
        decimal deltaPercentage,
        string timeHorizon)
    {
        string direction = deltaPercentage >= 0 ? "expansion" : "contraction";
        string sign = deltaPercentage >= 0 ? "+" : "";

        return new ScenarioAnalysisNarrative(
            Summary: $"Under the modeled assumptions over the {timeHorizon}, {targetMetric} is projected to exhibit a net {direction} of {sign}{deltaPercentage:F1}%. The expected trajectory indicates sensitivity to primary demand levers, with upper-bound performance offering strategic upside if execution tailwinds persist.",
            KeyDrivers: new[]
            {
                $"Compound growth sensitivity across primary variable levers accounts for the {sign}{deltaPercentage:F1}% trajectory shift.",
                $"Operational elasticity between volume growth and margin compression defines the upper and lower sensitivity bounds."
            },
            RiskFactors: new[]
            {
                "Adverse macro headwinds or unanticipated churn acceleration would drag performance toward the stress-test lower bound.",
                "Variable elasticity assumptions may deviate if competitive pricing pressure intensifies."
            },
            UpsideCatalysts: new[]
            {
                "Accelerated customer retention or cross-sell velocity could unlock the optimistic best-case scenario trajectory.",
                "Economies of scale and operational leverage provide potential margin expansion."
            },
            StrategicRecommendations: new[]
            {
                "Establish early warning thresholds on high-weight variables to detect early deviation from the expected case.",
                "Stress-test cash flow reserves against the worst-case trajectory before committing to unhedged capital expenditure."
            });
    }

    private static string StripMarkdownFences(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return "{}";
        string trimmed = content.Trim();
        if (trimmed.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed[7..];
        else if (trimmed.StartsWith("```"))
            trimmed = trimmed[3..];
        if (trimmed.EndsWith("```"))
            trimmed = trimmed[..^3];
        return trimmed.Trim();
    }
}
