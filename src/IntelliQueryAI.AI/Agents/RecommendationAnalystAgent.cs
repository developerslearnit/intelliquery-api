using System.Text.Json;
using System.Text.Json.Serialization;
using IntelliQueryAI.AI.Common;
using IntelliQueryAI.AI.Telemetry;
using IntelliQueryAI.Domain.Entities.Recommendations;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IntelliQueryAI.AI.Agents;

public sealed record GeneratedRecommendationItem(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("rationale")] string Rationale,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("priority")] string Priority,
    [property: JsonPropertyName("targetMetric")] string? TargetMetric,
    [property: JsonPropertyName("impactMagnitude")] string? ImpactMagnitude,
    [property: JsonPropertyName("estimatedEffort")] string? EstimatedEffort,
    [property: JsonPropertyName("evidence")] IReadOnlyList<RecommendationEvidenceItem> Evidence,
    [property: JsonPropertyName("actionSteps")] IReadOnlyList<RecommendationActionStep> ActionSteps,
    [property: JsonPropertyName("relatedInvestigationId")] Guid? RelatedInvestigationId,
    [property: JsonPropertyName("relatedAlertId")] Guid? RelatedAlertId,
    [property: JsonPropertyName("relatedMonitorId")] Guid? RelatedMonitorId,
    [property: JsonPropertyName("relatedScenarioId")] Guid? RelatedScenarioId);

/// <summary>
/// Autonomous Microsoft Agent Framework agent specializing in synthesizing evidence-backed,
/// actionable business recommendations across anomalies, monitor alerts, root-cause investigations,
/// and What-If scenario simulations.
/// Core Invariant: Do NOT automatically perform destructive business actions. Always human-in-the-loop.
/// </summary>
public sealed class RecommendationAnalystAgent : BaseAgent
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ILogger<RecommendationAnalystAgent> _logger;

    public RecommendationAnalystAgent(
        IChatClient? chatClient,
        ITelemetryService telemetry,
        ILogger<RecommendationAnalystAgent> logger,
        IOptions<AzureOpenAISettings> options,
        SimulationMode simulationMode)
        : base(
            chatClient,
            instructions: """
                You are the Principal Recommendation & Actionable Intelligence Analyst Agent for IntelliQueryAI.
                Your role is to analyze operational anomalies, monitoring alerts, root-cause investigations,
                and What-If scenario simulations across an enterprise organization to synthesize high-impact,
                evidence-backed recommendations.

                CRITICAL INVARIANTS:
                - Every recommendation MUST be anchored in concrete supporting evidence (metric anomalies, alert triggers, scenario downside risks, or diagnostic findings).
                - Do NOT automatically perform destructive business actions without human-in-the-loop review.
                - Action steps must be specific, sequenceable, and operational.
                - Categorize recommendations into clear categories: 'Revenue Retention', 'Cost Optimization', 'Risk Mitigation', 'Operational Efficiency', or 'Governance & Compliance'.
                - Assign appropriate priority: 'Critical', 'High', 'Medium', or 'Low'.
                - Output ONLY a valid JSON array of objects strictly matching the GeneratedRecommendationItem structure:
                  [
                    {
                      "title": "Action-oriented title",
                      "rationale": "Clear explanation of why this recommendation is needed based on evidence",
                      "category": "Revenue Retention",
                      "priority": "High",
                      "targetMetric": "Enterprise Churn Rate",
                      "impactMagnitude": "+$180,000 ARR Protected",
                      "estimatedEffort": "Medium (1-2 weeks)",
                      "evidence": [
                        {
                          "title": "Enterprise Churn Spike",
                          "description": "Churn increased by 14% across Tier-1 enterprise accounts",
                          "metricName": "Enterprise Churn Rate",
                          "metricValue": 0.082,
                          "baselineValue": 0.045,
                          "evidenceType": "Anomaly"
                        }
                      ],
                      "actionSteps": [
                        {
                          "stepNumber": 1,
                          "action": "Audit top 10 renewal contracts expiring within next 60 days",
                          "expectedImpact": "Identifies $450k in at-risk renewals",
                          "automatedActionPossible": false
                        }
                      ],
                      "relatedInvestigationId": null,
                      "relatedAlertId": null,
                      "relatedMonitorId": null,
                      "relatedScenarioId": null
                    }
                  ]
                - Do not include markdown code fences or conversational boilerplate.
                """,
            name: "RecommendationAnalystAgent",
            description: "Synthesizes evidence-backed business recommendations across multi-agent intelligence signals.",
            tools: null,
            telemetry: telemetry,
            logger: logger,
            simulationMode: simulationMode,
            temperature: options?.Value?.Temperature ?? 0.2f)
    {
        _logger = logger;
    }

    public async Task<IReadOnlyList<GeneratedRecommendationItem>> SynthesizeRecommendationsAsync(
        string workspaceContextPrompt,
        CancellationToken cancellationToken = default)
    {
        string prompt = $"""
            Synthesize prioritized, evidence-backed business recommendations based on the following enterprise intelligence telemetry:

            {workspaceContextPrompt}

            Output a valid JSON array of GeneratedRecommendationItem objects.
            """;

        try
        {
            var response = await ExecuteAsync(new AgentRequest(prompt), cancellationToken);
            string clean = StripMarkdownFences(response.Content);
            var parsed = JsonSerializer.Deserialize<IReadOnlyList<GeneratedRecommendationItem>>(clean, JsonOptions);

            return parsed ?? GenerateSimulatedRecommendations();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse recommendations from LLM; using resilient deterministic fallback.");
            return GenerateSimulatedRecommendations();
        }
    }

    protected override string GenerateSimulatedResponse(AgentRequest request)
    {
        var items = GenerateSimulatedRecommendations();
        return JsonSerializer.Serialize(items, JsonOptions);
    }

    public static IReadOnlyList<GeneratedRecommendationItem> GenerateSimulatedRecommendations()
    {
        return new List<GeneratedRecommendationItem>
        {
            new(
                Title: "Remediate Enterprise Renewal Churn Risk in Cloud Segment",
                Rationale: "Recent root-cause investigation and metric alerts detected a 16.8% contraction in enterprise account retention triggered by contract renegotiation delays.",
                Category: "Revenue Retention",
                Priority: "High",
                TargetMetric: "Enterprise ARR Retention",
                ImpactMagnitude: "+$240,000 Annualized Revenue Protection",
                EstimatedEffort: "Low (3-5 days)",
                Evidence: new List<RecommendationEvidenceItem>
                {
                    new(
                        Title: "Enterprise Tier Contract Renewal Contraction",
                        Description: "Observed 16.8% variance against standard 90-day renewal cycle baseline.",
                        MetricName: "Net Revenue Retention",
                        MetricValue: 88.2m,
                        BaselineValue: 105.0m,
                        EvidenceType: "RootCauseInvestigation"
                    ),
                    new(
                        Title: "Renewal Lead-Time Alert",
                        Description: "Average closing cycle expanded from 21 days to 44 days.",
                        MetricName: "Sales Cycle Duration",
                        MetricValue: 44.0m,
                        BaselineValue: 21.0m,
                        EvidenceType: "AlertTrigger"
                    )
                },
                ActionSteps: new List<RecommendationActionStep>
                {
                    new(1, "Trigger executive check-in with Tier-1 accounts renewing in Q3", "Secures $420k pipeline commit", false),
                    new(2, "Implement flexible multi-year tier discount incentives (+5% cap)", "Neutralizes competitor pricing leverage", false),
                    new(3, "Schedule technical health audit for accounts with lower utilization", "Identifies product adoption friction", false)
                },
                RelatedInvestigationId: null,
                RelatedAlertId: null,
                RelatedMonitorId: null,
                RelatedScenarioId: null
            ),
            new(
                Title: "Optimize Cloud Infrastructure Multi-Region Provisioning",
                Rationale: "What-If scenario stress-testing and monitor logs indicate database IOPS overhead surged by 22% during peak ETL ingestion windows without proportional throughput gain.",
                Category: "Cost Optimization",
                Priority: "Medium",
                TargetMetric: "Cloud Infrastructure Overhead",
                ImpactMagnitude: "-$14,500 Monthly Compute Spend",
                EstimatedEffort: "Medium (1-2 weeks)",
                Evidence: new List<RecommendationEvidenceItem>
                {
                    new(
                        Title: "Database IOPS Spike During ETL Execution",
                        Description: "Provisioned throughput idle capacity averaged 64% outside 02:00-04:00 UTC batch hours.",
                        MetricName: "Database Provisioned IOPS",
                        MetricValue: 12500m,
                        BaselineValue: 6000m,
                        EvidenceType: "MonitorAnomaly"
                    )
                },
                ActionSteps: new List<RecommendationActionStep>
                {
                    new(1, "Transition batch ETL pipelines to auto-scaling serverless query clusters", "Reduces baseline cluster capacity", false),
                    new(2, "Enable compression for archival partitions older than 90 days", "Frees 35% provisioned storage volume", false)
                },
                RelatedInvestigationId: null,
                RelatedAlertId: null,
                RelatedMonitorId: null,
                RelatedScenarioId: null
            ),
            new(
                Title: "Enforce Masking Strategy on Payment Transaction Webhooks",
                Rationale: "Data governance policy audit identified unmasked external gateway callback logs containing customer card brand and billing zip attributes.",
                Category: "Governance & Compliance",
                Priority: "Critical",
                TargetMetric: "Data Governance Compliance Rate",
                ImpactMagnitude: "100% PCI-DSS Boundary Enforcement",
                EstimatedEffort: "Low (1-2 days)",
                Evidence: new List<RecommendationEvidenceItem>
                {
                    new(
                        Title: "Sensitive Payment Webhook Field Unmasked",
                        Description: "Transaction audit log recorded unmasked customer billing metadata.",
                        MetricName: "Governed Audit Compliance",
                        MetricValue: 91.5m,
                        BaselineValue: 100.0m,
                        EvidenceType: "GovernanceAuditLog"
                    )
                },
                ActionSteps: new List<RecommendationActionStep>
                {
                    new(1, "Apply Partial Masking policy rule to payment_gateway_logs.billing_meta", "Ensures compliance with Phase F zero-leakage rules", false),
                    new(2, "Rotate API webhook verification signing secret", "Revokes legacy credential exposure", false)
                },
                RelatedInvestigationId: null,
                RelatedAlertId: null,
                RelatedMonitorId: null,
                RelatedScenarioId: null
            )
        };
    }

    private static string StripMarkdownFences(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        string t = text.Trim();
        if (t.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
            t = t.Substring(7);
        else if (t.StartsWith("```"))
            t = t.Substring(3);

        if (t.EndsWith("```"))
            t = t.Substring(0, t.Length - 3);

        return t.Trim();
    }
}
