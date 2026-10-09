using System.Text.Json;
using System.Text.Json.Serialization;
using IntelliQueryAI.AI.Common;
using IntelliQueryAI.AI.Telemetry;
using IntelliQueryAI.Domain.Entities.Executive;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IntelliQueryAI.AI.Agents;

public sealed record InvestigationAgentContributor(
    [property: JsonPropertyName("dimension")] string Dimension,
    [property: JsonPropertyName("segment")] string Segment,
    [property: JsonPropertyName("contributionPercentage")] double ContributionPercentage,
    [property: JsonPropertyName("impactDirection")] string ImpactDirection,
    [property: JsonPropertyName("baselineValue")] decimal BaselineValue,
    [property: JsonPropertyName("currentValue")] decimal CurrentValue,
    [property: JsonPropertyName("changeValue")] decimal ChangeValue,
    [property: JsonPropertyName("explanation")] string Explanation);

public sealed record InvestigationAgentEvidence(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("evidenceType")] string EvidenceType,
    [property: JsonPropertyName("metricName")] string? MetricName,
    [property: JsonPropertyName("dataPointsJson")] string? DataPointsJson,
    [property: JsonPropertyName("chartType")] string? ChartType);

public sealed record InvestigationAgentRecommendation(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("priority")] string Priority,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("rationale")] string Rationale,
    [property: JsonPropertyName("actionSteps")] IReadOnlyList<string>? ActionSteps);

public sealed record InvestigationAgentFindings(
    [property: JsonPropertyName("summary")] string Summary,
    [property: JsonPropertyName("confidence")] double Confidence,
    [property: JsonPropertyName("rootCauses")] IReadOnlyList<string> RootCauses,
    [property: JsonPropertyName("contributors")] IReadOnlyList<InvestigationAgentContributor> Contributors,
    [property: JsonPropertyName("evidence")] IReadOnlyList<InvestigationAgentEvidence> Evidence,
    [property: JsonPropertyName("recommendations")] IReadOnlyList<InvestigationAgentRecommendation> Recommendations,
    [property: JsonPropertyName("followUpQuestions")] IReadOnlyList<string> FollowUpQuestions);

/// <summary>
/// Autonomous Root-Cause Investigation Agent.
/// Uses Microsoft Agent Framework to reason across dimension breakdowns, trend deviations, and anomaly evidence.
/// </summary>
public sealed class RootCauseInvestigationAgent : BaseAgent
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public RootCauseInvestigationAgent(
        IChatClient? chatClient,
        ITelemetryService telemetry,
        ILogger<RootCauseInvestigationAgent> logger,
        IOptions<AzureOpenAISettings> options,
        SimulationMode simulationMode)
        : base(
            chatClient,
            instructions: """
                You are the Principal AI Root-Cause Investigation Agent for the IntelliQueryAI enterprise decision platform.
                Your role is to investigate diagnostic business questions (e.g., "Why did revenue drop last month?", "What caused the customer churn spike?").
                
                You are provided with:
                1. The diagnostic user question
                2. Relevant business definitions & metrics (Semantic Layer)
                3. Discovered dimensions, baseline vs. current metrics, and observed segment variations
                4. Any detected statistical anomalies or query execution evidence
                
                Your task:
                1. Synthesize an evidence-driven, clear executive summary of what happened and why.
                2. Formulate explicit, high-conviction Root Causes.
                3. Rank major contributors by dimension and segment with quantified impact percentage and change.
                4. Collate supporting evidence (e.g. Metric deviations, dimension breakdowns) suitable for visualization.
                5. Provide concrete, prioritized, actionable recommendations with distinct steps.
                6. Formulate relevant analytical follow-up questions for deeper investigation.
                
                CRITICAL RULES:
                - Never output internal raw GUIDs or database technical hashes in explanations.
                - Every conclusion must be grounded in the provided evidence.
                - Output strictly valid JSON matching the schema below. Do not wrap in markdown or add conversational filler.
                
                Expected JSON format:
                {
                  "summary": "Revenue contracted by 14.2% primarily driven by mid-market customer tier churn and payment processor API timeout spikes.",
                  "confidence": 0.91,
                  "rootCauses": [
                    "Failure rate in EU Stripe payment gateway increased to 12.4% during peak billing week.",
                    "3 key Enterprise tier accounts downgraded their subscription tier following contract expiration."
                  ],
                  "contributors": [
                    {
                      "dimension": "CustomerTier",
                      "segment": "Mid-Market",
                      "contributionPercentage": -62.5,
                      "impactDirection": "Negative",
                      "baselineValue": 240000.0,
                      "currentValue": 90000.0,
                      "changeValue": -150000.0,
                      "explanation": "Account downgrades concentrated in the EU region during contract renewals."
                    }
                  ],
                  "evidence": [
                    {
                      "title": "Payment Failure Volatility",
                      "description": "Sharp deviation in transaction success rate coinciding with API timeouts.",
                      "evidenceType": "AnomalyCorrelation",
                      "metricName": "TransactionFailureRate",
                      "dataPointsJson": "[{\"period\":\"Day 1\",\"value\":1.2},{\"period\":\"Day 14\",\"value\":12.4}]",
                      "chartType": "Line"
                    }
                  ],
                  "recommendations": [
                    {
                      "title": "Implement Gateway Failover & Webhook Retry",
                      "description": "Configure secondary payment routing for EU transaction processing.",
                      "priority": "Critical",
                      "category": "Infrastructure",
                      "rationale": "High concentration of billing errors originated from processor timeouts.",
                      "actionSteps": [
                        "Enable secondary PSP failover routing",
                        "Audit webhook retries for dropped transactions"
                      ]
                    }
                  ],
                  "followUpQuestions": [
                    "Which specific merchant accounts experienced repeated transaction timeouts?",
                    "How has support ticket turnaround time trended for impacted accounts?"
                  ]
                }
                """,
            name: "RootCauseInvestigationAgent",
            description: "Reasons across baseline trends, dimension breakdowns, and anomaly evidence to synthesize structured root-cause investigations.",
            tools: null,
            telemetry: telemetry,
            logger: logger,
            simulationMode: simulationMode,
            temperature: options?.Value?.Temperature ?? 0.2f)
    {
    }

    public InvestigationAgentFindings ParseFindings(string rawContent)
    {
        try
        {
            var cleaned = CleanJsonResponse(rawContent);
            var parsed = JsonSerializer.Deserialize<InvestigationAgentFindings>(cleaned, JsonOptions);
            if (parsed is not null && !string.IsNullOrWhiteSpace(parsed.Summary))
            {
                return parsed;
            }
        }
        catch
        {
            // Fallback to structured parsing if model returned partial text
        }

        return new InvestigationAgentFindings(
            Summary: string.IsNullOrWhiteSpace(rawContent)
                ? "Investigation concluded with insufficient signal to isolate definitive root causes."
                : rawContent.Trim(),
            Confidence: 0.75,
            RootCauses: new[] { "Statistical variance detected across primary metric dimensions." },
            Contributors: Array.Empty<InvestigationAgentContributor>(),
            Evidence: Array.Empty<InvestigationAgentEvidence>(),
            Recommendations: Array.Empty<InvestigationAgentRecommendation>(),
            FollowUpQuestions: new[] { "Would you like to analyze this trend across a wider date range?" });
    }

    protected override string GenerateSimulatedResponse(AgentRequest request)
    {
        return """
            {
              "summary": "Deep investigation reveals that the observed variance is primarily driven by segment concentration in the primary operational dimension, compounded by isolated latency anomalies during peak transaction hours.",
              "confidence": 0.92,
              "rootCauses": [
                "Disproportionate volume contraction (-68.4%) within the primary customer tier.",
                "Elevated service response latency during peak reporting intervals resulting in deferred checkouts."
              ],
              "contributors": [
                {
                  "dimension": "CustomerSegment",
                  "segment": "Enterprise",
                  "contributionPercentage": -58.2,
                  "impactDirection": "Negative",
                  "baselineValue": 450000.0,
                  "currentValue": 188000.0,
                  "changeValue": -262000.0,
                  "explanation": "Renewal delays and contraction in 4 key accounts accounted for the majority of the metric drop."
                },
                {
                  "dimension": "Region",
                  "segment": "North America",
                  "contributionPercentage": -31.5,
                  "impactDirection": "Negative",
                  "baselineValue": 310000.0,
                  "currentValue": 212000.0,
                  "changeValue": -98000.0,
                  "explanation": "Regional volume softening coincided with broader macroeconomic contract renegotiations."
                },
                {
                  "dimension": "ProductLine",
                  "segment": "Premium Add-ons",
                  "contributionPercentage": 12.4,
                  "impactDirection": "Positive",
                  "baselineValue": 80000.0,
                  "currentValue": 115000.0,
                  "changeValue": 35000.0,
                  "explanation": "Strong adoption of AI workspace tier partially offset core subscription contractions."
                }
              ],
              "evidence": [
                {
                  "title": "Period-over-Period Metric Baseline",
                  "description": "Baseline volume tracked steadily before diverging sharply in the final two periods.",
                  "evidenceType": "MetricChange",
                  "metricName": "PrimaryVolume",
                  "dataPointsJson": "[{\"period\":\"Prior P1\",\"value\":420000},{\"period\":\"Prior P2\",\"value\":435000},{\"period\":\"Current P1\",\"value\":310000},{\"period\":\"Current P2\",\"value\":225000}]",
                  "chartType": "Line"
                },
                {
                  "title": "Dimensional Breakdown by Customer Segment",
                  "description": "Enterprise tier accounts for 74% of the net downward variance.",
                  "evidenceType": "DimensionBreakdown",
                  "metricName": "SegmentContribution",
                  "dataPointsJson": "[{\"segment\":\"Enterprise\",\"value\":-262000},{\"segment\":\"Mid-Market\",\"value\":-45000},{\"segment\":\"SMB\",\"value\":-12000}]",
                  "chartType": "Bar"
                }
              ],
              "recommendations": [
                {
                  "title": "Proactive Account Renewal Review",
                  "description": "Conduct executive stakeholder sessions for top 10 accounts entering renewal in the next 60 days.",
                  "priority": "Critical",
                  "category": "CustomerSuccess",
                  "rationale": "Over 58% of the negative variance stemmed from a small cluster of delayed renewals.",
                  "actionSteps": [
                    "Flag at-risk renewal opportunities in CRM",
                    "Schedule executive sponsor alignment calls",
                    "Offer flexible annual payment schedule"
                  ]
                },
                {
                  "title": "Optimize Checkout Flow & API Latency",
                  "description": "Remediate service bottlenecks identified during peak traffic intervals.",
                  "priority": "High",
                  "category": "Operations",
                  "rationale": "Anomalous timeout spikes correlated directly with transaction drop-offs.",
                  "actionSteps": [
                    "Scale database connection pool for checkout services",
                    "Review external webhook retry queues"
                  ]
                }
              ],
              "followUpQuestions": [
                "Which specific client accounts represented the largest renewal delays?",
                "How does the churn rate correlate with product usage intensity over the last 90 days?",
                "What was the impact on net revenue retention (NRR)?"
              ]
            }
            """;
    }

    private static string CleanJsonResponse(string response)
    {
        var cleaned = response.Trim();
        if (cleaned.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
        {
            cleaned = cleaned[7..];
        }
        else if (cleaned.StartsWith("```"))
        {
            cleaned = cleaned[3..];
        }

        if (cleaned.EndsWith("```"))
        {
            cleaned = cleaned[..^3];
        }

        return cleaned.Trim();
    }
}

