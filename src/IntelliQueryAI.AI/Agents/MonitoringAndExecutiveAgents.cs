using System.Text.Json;
using System.Text.Json.Serialization;
using IntelliQueryAI.AI.Common;
using IntelliQueryAI.AI.Telemetry;
using IntelliQueryAI.Domain.Enums;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IntelliQueryAI.AI.Agents;

// ---------------------------------------------------------------------------
// 1. Monitoring Agent
// ---------------------------------------------------------------------------

public sealed record MonitoringAgentFindings(
    [property: JsonPropertyName("triggerAlert")] bool TriggerAlert,
    [property: JsonPropertyName("severity")] string Severity,
    [property: JsonPropertyName("findings")] string Findings,
    [property: JsonPropertyName("trendSummary")] string TrendSummary,
    [property: JsonPropertyName("recommendedActions")] IReadOnlyList<string>? RecommendedActions);

public sealed class MonitoringAgent : BaseAgent
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public MonitoringAgent(
        IChatClient? chatClient,
        ITelemetryService telemetry,
        ILogger<MonitoringAgent> logger,
        IOptions<AzureOpenAISettings> options,
        SimulationMode simulationMode)
        : base(
            chatClient,
            instructions: """
                You are the Monitoring Agent for the IntelliQueryAI platform.
                Your role is to evaluate AI Monitors against historical and current operational metrics.
                Analyze trends across time periods, evaluate configured monitor rules and business thresholds,
                and generate clear analytical findings.
                Determine whether an alert must be triggered, evaluate the severity (Information, Warning, Critical),
                and summarize the operational trend.
                Output strictly JSON adhering to the following structure:
                {
                    "triggerAlert": true,
                    "severity": "Warning",
                    "findings": "Revenue for the current period dropped 18.4% compared to the 30-day moving average.",
                    "trendSummary": "Downward trajectory across enterprise customer tier over the past 14 days.",
                    "recommendedActions": [
                        "Investigate top customer payment failures",
                        "Review regional enterprise subscription churn"
                    ]
                }
                """,
            name: "MonitoringAgent",
            description: "Evaluates automated metric monitors, verifies threshold breaches, and generates trend findings.",
            tools: null,
            telemetry: telemetry,
            logger: logger,
            simulationMode: simulationMode,
            temperature: options?.Value?.Temperature)
    {
    }

    public MonitoringAgentFindings ParseFindings(string rawContent)
    {
        try
        {
            var cleaned = CleanJsonResponse(rawContent);
            var parsed = JsonSerializer.Deserialize<MonitoringAgentFindings>(cleaned, JsonOptions);
            if (parsed is not null)
            {
                return parsed;
            }
        }
        catch
        {
            // Fallback to structured wrapper if LLM returned plain text
        }

        return new MonitoringAgentFindings(
            TriggerAlert: false,
            Severity: "Information",
            Findings: rawContent.Trim(),
            TrendSummary: "Metric observed within historical parameters.",
            RecommendedActions: new List<string>());
    }

    protected override string GenerateSimulatedResponse(AgentRequest request)
    {
        return """
            {
                "triggerAlert": false,
                "severity": "Information",
                "findings": "Metric evaluated successfully. Observed values track closely within the 95% confidence interval of historical baseline.",
                "trendSummary": "Stable trajectory with low volatility across measured historical periods.",
                "recommendedActions": [
                    "Continue automated scheduled monitoring",
                    "Maintain standard operational baselines"
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

// ---------------------------------------------------------------------------
// 2. Anomaly Agent
// ---------------------------------------------------------------------------

public sealed record AnomalyClassification(
    [property: JsonPropertyName("isAnomaly")] bool IsAnomaly,
    [property: JsonPropertyName("severity")] string Severity,
    [property: JsonPropertyName("anomalyType")] string AnomalyType,
    [property: JsonPropertyName("patternDescription")] string PatternDescription,
    [property: JsonPropertyName("confidence")] double Confidence,
    [property: JsonPropertyName("riskAssessment")] string RiskAssessment);

public sealed class AnomalyAgent : BaseAgent
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public AnomalyAgent(
        IChatClient? chatClient,
        ITelemetryService telemetry,
        ILogger<AnomalyAgent> logger,
        IOptions<AzureOpenAISettings> options,
        SimulationMode simulationMode)
        : base(
            chatClient,
            instructions: """
                You are the Anomaly Agent for the IntelliQueryAI platform.
                Your task is to examine metric sequences and statistical indicators to identify unusual business patterns.
                Detect:
                1. Sudden revenue drops or surges
                2. Customer dormancy or unexpected churn spikes
                3. Tax collection aberrations or rate discrepancies
                4. Payment gateway failure clusters
                Classify severity as 'Information', 'Warning', or 'Critical'.
                Assess the underlying risk and assign a confidence score between 0.0 and 1.0.
                Output strictly JSON conforming to:
                {
                    "isAnomaly": true,
                    "severity": "Critical",
                    "anomalyType": "SuddenRevenueDrop",
                    "patternDescription": "Detected sudden 24.5% decline in transaction volume within 2 hours.",
                    "confidence": 0.94,
                    "riskAssessment": "Potential payment gateway connectivity degradation or upstream processing disruption."
                }
                """,
            name: "AnomalyAgent",
            description: "Detects aberrant metric patterns, classifies anomaly severity, and estimates operational risk.",
            tools: null,
            telemetry: telemetry,
            logger: logger,
            simulationMode: simulationMode,
            temperature: options?.Value?.Temperature)
    {
    }

    public AnomalyClassification ParseClassification(string rawContent)
    {
        try
        {
            var cleaned = CleanJsonResponse(rawContent);
            var parsed = JsonSerializer.Deserialize<AnomalyClassification>(cleaned, JsonOptions);
            if (parsed is not null)
            {
                return parsed;
            }
        }
        catch
        {
            // Fallback
        }

        return new AnomalyClassification(
            IsAnomaly: false,
            Severity: "Information",
            AnomalyType: "Normal",
            PatternDescription: rawContent.Trim(),
            Confidence: 0.8,
            RiskAssessment: "No immediate operational risk detected.");
    }

    protected override string GenerateSimulatedResponse(AgentRequest request)
    {
        return """
            {
                "isAnomaly": false,
                "severity": "Information",
                "anomalyType": "NormalPattern",
                "patternDescription": "Observed distribution aligns with standard statistical expectations without anomalous variance.",
                "confidence": 0.95,
                "riskAssessment": "Risk level low. Normal operational status."
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

// ---------------------------------------------------------------------------
// 3. Executive Summary Agent
// ---------------------------------------------------------------------------

public sealed record ExecutiveSummaryBriefing(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("reportPeriod")] string ReportPeriod,
    [property: JsonPropertyName("summary")] string Summary,
    [property: JsonPropertyName("keyFindings")] IReadOnlyList<string> KeyFindings,
    [property: JsonPropertyName("risks")] IReadOnlyList<string> Risks,
    [property: JsonPropertyName("opportunities")] IReadOnlyList<string> Opportunities);

public sealed class ExecutiveSummaryAgent : BaseAgent
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ExecutiveSummaryAgent(
        IChatClient? chatClient,
        ITelemetryService telemetry,
        ILogger<ExecutiveSummaryAgent> logger,
        IOptions<AzureOpenAISettings> options,
        SimulationMode simulationMode)
        : base(
            chatClient,
            instructions: """
                You are the Executive Summary Agent for the IntelliQueryAI platform.
                Your role is to generate C-suite executive briefings that synthesize enterprise data, query history,
                active monitors, and anomaly alerts into clear executive intelligence.
                Always refer to the business or workspace by its friendly name (e.g., 'Second WorkSpace') or 'the organization/enterprise', and NEVER output internal database GUIDs/IDs in the narrative summary, findings, risks, or recommendations.
                Always generate:
                1. A high-level Executive Summary (1-2 executive paragraphs)
                2. Key Findings (bulleted concise analytical conclusions)
                3. Risks (operational, financial, or retention risks)
                4. Strategic Opportunities (revenue growth, cost optimization, or efficiency gains)
                Output strictly JSON adhering to:
                {
                    "title": "Monthly Executive Intelligence Briefing",
                    "reportPeriod": "October 2026",
                    "summary": "Enterprise revenue maintained a healthy 14% growth trajectory, despite localized churn in mid-tier accounts.",
                    "keyFindings": ["Gross volume increased to $1.2M", "Database query throughput increased 35%"],
                    "risks": ["Mid-tier subscription cancellation increased 4.2%", "Higher payment gateway latency observed in EU region"],
                    "opportunities": ["Expansion into EMEA markets shows 22% higher ARPU", "Automated indexing reduced query cost"]
                }
                """,
            name: "ExecutiveSummaryAgent",
            description: "Produces executive-ready business briefings highlighting performance, risks, and opportunities.",
            tools: null,
            telemetry: telemetry,
            logger: logger,
            simulationMode: simulationMode,
            temperature: options?.Value?.Temperature)
    {
    }

    public ExecutiveSummaryBriefing ParseBriefing(string rawContent)
    {
        try
        {
            var cleaned = CleanJsonResponse(rawContent);
            var parsed = JsonSerializer.Deserialize<ExecutiveSummaryBriefing>(cleaned, JsonOptions);
            if (parsed is not null)
            {
                return parsed;
            }
        }
        catch
        {
            // Fallback
        }

        return new ExecutiveSummaryBriefing(
            Title: "Executive Intelligence Summary",
            ReportPeriod: DateTime.UtcNow.ToString("MMMM yyyy"),
            Summary: rawContent.Trim(),
            KeyFindings: new List<string> { "Platform metrics collected and summarized." },
            Risks: new List<string>(),
            Opportunities: new List<string>());
    }

    protected override string GenerateSimulatedResponse(AgentRequest request)
    {
        return """
            {
                "title": "Monthly Business Intelligence Summary",
                "reportPeriod": "Current Period",
                "summary": "Core performance metrics demonstrate robust resilience with steady volume growth across connected data sources. AI query latency remains well within target service level agreements.",
                "keyFindings": [
                    "Query throughput expanded 28% period-over-period with 99.8% AST security gate success.",
                    "Monitored revenue indicators reflect sustained expansion across key customer segments.",
                    "No critical unacknowledged anomalies detected across active production monitors."
                ],
                "risks": [
                    "Monitor execution frequency may require higher capacity provisioning as workspace expands.",
                    "Payment failure alerts flagged 2 minor retry delays during peak transaction hours."
                ],
                "opportunities": [
                    "Leverage automated alerts for proactive account retention prior to scheduled renewal dates.",
                    "Deploy specialized AI monitors across supplementary enterprise financial tables."
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

// ---------------------------------------------------------------------------
// 4. Recommendation Agent
// ---------------------------------------------------------------------------

public sealed record ActionableRecommendation(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("priority")] string Priority,
    [property: JsonPropertyName("rationale")] string Rationale,
    [property: JsonPropertyName("actionSteps")] IReadOnlyList<string> ActionSteps);

public sealed record RecommendationResult(
    [property: JsonPropertyName("recommendations")] IReadOnlyList<ActionableRecommendation> Recommendations);

public sealed class RecommendationAgent : BaseAgent
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public RecommendationAgent(
        IChatClient? chatClient,
        ITelemetryService telemetry,
        ILogger<RecommendationAgent> logger,
        IOptions<AzureOpenAISettings> options,
        SimulationMode simulationMode)
        : base(
            chatClient,
            instructions: """
                You are the Recommendation Agent for the IntelliQueryAI platform.
                Your task is to formulate high-impact, prescriptive recommendations based on detected anomalies,
                monitor findings, and executive risks.
                Provide structured actionable recommendations with:
                1. Clear Title
                2. Category (e.g. Revenue, Retention, Performance, Governance)
                3. Priority ('High', 'Medium', 'Low')
                4. Rationale
                5. Step-by-step action plan
                Output strictly JSON adhering to:
                {
                    "recommendations": [
                        {
                            "title": "Launch Proactive Enterprise Retention Campaign",
                            "category": "Retention",
                            "priority": "High",
                            "rationale": "Enterprise churn increased 4.2% following last month's platform upgrade.",
                            "actionSteps": [
                                "Schedule executive business reviews with top 10 accounts",
                                "Provide customized retention incentives for renewal dates within 60 days"
                            ]
                        }
                    ]
                }
                """,
            name: "RecommendationAgent",
            description: "Prescribes corrective actions and business recommendations based on analytical findings and alerts.",
            tools: null,
            telemetry: telemetry,
            logger: logger,
            simulationMode: simulationMode,
            temperature: options?.Value?.Temperature)
    {
    }

    public IReadOnlyList<ActionableRecommendation> ParseRecommendations(string rawContent)
    {
        try
        {
            var cleaned = CleanJsonResponse(rawContent);
            var parsed = JsonSerializer.Deserialize<RecommendationResult>(cleaned, JsonOptions);
            if (parsed?.Recommendations is not null && parsed.Recommendations.Count > 0)
            {
                return parsed.Recommendations;
            }
        }
        catch
        {
            // Fallback
        }

        return new List<ActionableRecommendation>
        {
            new(
                Title: "Review Active Operational Monitors",
                Category: "Governance",
                Priority: "Medium",
                Rationale: "Periodic governance review ensures threshold sensitivity remains aligned with business changes.",
                ActionSteps: new List<string> { "Audit configured monitor rules", "Verify alert recipient notifications" })
        };
    }

    protected override string GenerateSimulatedResponse(AgentRequest request)
    {
        return """
            {
                "recommendations": [
                    {
                        "title": "Establish Automated Revenue Anomaly Alerting",
                        "category": "Revenue",
                        "priority": "High",
                        "rationale": "Automated Z-score thresholding flags abnormal fluctuations before end-of-month reconciliations.",
                        "actionSteps": [
                            "Configure daily AI Revenue Monitor on core billing data source",
                            "Enable instant critical severity alerts for deviations exceeding 2.5 sigma"
                        ]
                    },
                    {
                        "title": "Conduct Regional Performance Audit",
                        "category": "Performance",
                        "priority": "Medium",
                        "rationale": "Slight regional variances observed in transaction processing times.",
                        "actionSteps": [
                            "Profile slow-performing query filters across database read replicas",
                            "Review network throughput across distributed cloud regions"
                        ]
                    }
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
