using System.Text.Json;
using System.Text.Json.Serialization;
using IntelliQueryAI.AI.Common;
using IntelliQueryAI.AI.Telemetry;
using IntelliQueryAI.Domain.Entities.Dashboards;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IntelliQueryAI.AI.Agents;

public sealed record DashboardAgentWidget(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("widgetType")] string WidgetType,
    [property: JsonPropertyName("metric")] string Metric,
    [property: JsonPropertyName("dimension")] string? Dimension = null,
    [property: JsonPropertyName("aggregation")] string? Aggregation = "SUM",
    [property: JsonPropertyName("sqlQuery")] string? SqlQuery = null,
    [property: JsonPropertyName("dateRange")] string? DateRange = null,
    [property: JsonPropertyName("colSpan")] int ColSpan = 1,
    [property: JsonPropertyName("rowSpan")] int RowSpan = 1,
    [property: JsonPropertyName("options")] string? Options = null);

public sealed record DashboardAgentResult(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("dateRange")] string DateRange,
    [property: JsonPropertyName("widgets")] IReadOnlyList<DashboardAgentWidget> Widgets);

/// <summary>
/// Autonomous AI Dashboard Generator Agent.
/// Formulates safe structured dashboard definitions from natural language prompts.
/// Enforces widget type whitelisting and strictly prevents arbitrary script execution.
/// </summary>
public sealed class DashboardGeneratorAgent : BaseAgent
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ILogger<DashboardGeneratorAgent> _logger;

    public DashboardGeneratorAgent(
        IChatClient? chatClient,
        ITelemetryService telemetry,
        ILogger<DashboardGeneratorAgent> logger,
        IOptions<AzureOpenAISettings> options,
        SimulationMode simulationMode)
        : base(
            chatClient,
            instructions: """
                You are the Principal AI Dashboard Generator Agent for IntelliQueryAI.
                Your role is to translate user requests (e.g., "Create an executive revenue dashboard", "Show customer growth and retention")
                into a safe, structured, production-ready dashboard definition.

                CRITICAL SAFETY & GOVERNANCE RULES:
                - NEVER output arbitrary JavaScript, React, HTML, or executable frontend code.
                - Output ONLY valid JSON adhering strictly to the DashboardAgentResult structure.
                - Supported widget types (WHITELIST ONLY):
                    • "metric" (Single KPI card)
                    • "lineChart" (Time-series trend)
                    • "barChart" (Categorical comparison)
                    • "areaChart" (Cumulative or volume distribution)
                    • "pieChart" (Composition breakdown)
                    • "table" (Tabular dataset listing)
                - Formulate safe, read-only SELECT SQL queries for each widget when database schema context is provided.
                - Select sensible grid spans: metrics are typically colSpan 1; line/bar charts are colSpan 2; complex tables are colSpan 2 or 4.
                - Design an executive layout with 4 to 8 widgets offering balanced analytical depth.
                """,
            name: "DashboardGeneratorAgent",
            description: "Formulates safe structured dashboard definitions from natural language prompts.",
            tools: null,
            telemetry: telemetry,
            logger: logger,
            simulationMode: simulationMode,
            temperature: options?.Value?.Temperature ?? 0.2f)
    {
        _logger = logger;
    }

    public DashboardAgentResult ParseDashboardResult(string responseContent)
    {
        if (string.IsNullOrWhiteSpace(responseContent))
        {
            return BuildFallbackDashboard("Analytical Executive Dashboard", "Standard business overview.");
        }

        try
        {
            string clean = responseContent.Trim();
            if (clean.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
            {
                clean = clean.Substring(7);
            }
            else if (clean.StartsWith("```", StringComparison.OrdinalIgnoreCase))
            {
                clean = clean.Substring(3);
            }

            if (clean.EndsWith("```", StringComparison.OrdinalIgnoreCase))
            {
                clean = clean.Substring(0, clean.Length - 3);
            }

            clean = clean.Trim();

            var parsed = JsonSerializer.Deserialize<DashboardAgentResult>(clean, JsonOptions);
            if (parsed is not null && parsed.Widgets is not null && parsed.Widgets.Count > 0)
            {
                // Sanitize and validate every widget type against supported whitelist
                var validatedWidgets = parsed.Widgets.Select(w =>
                {
                    string safeType = DashboardWidgetValidator.NormalizeWidgetType(w.WidgetType);
                    return w with { WidgetType = safeType };
                }).ToList();

                return parsed with { Widgets = validatedWidgets };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse DashboardAgentResult JSON. Utilizing structured fallback generator.");
        }

        return BuildFallbackDashboard("AI Executive Dashboard", "Synthesized business analytics.");
    }

    protected override string GenerateSimulatedResponse(AgentRequest request)
    {
        string prompt = request.Prompt.ToLowerInvariant();

        if (prompt.Contains("revenue") || prompt.Contains("sales") || prompt.Contains("finance") || prompt.Contains("arr") || prompt.Contains("mrr"))
        {
            return JsonSerializer.Serialize(new DashboardAgentResult(
                Title: "Executive Revenue & Financial Performance",
                Description: "Real-time visibility into MRR growth, transaction volume, payment distributions, and revenue trajectories.",
                DateRange: "Last 30 Days",
                Widgets: new List<DashboardAgentWidget>
                {
                    new(
                        Title: "Total Revenue (MTD)",
                        WidgetType: "metric",
                        Metric: "Revenue",
                        Aggregation: "SUM",
                        SqlQuery: "SELECT COALESCE(SUM(amount), 142500.00) AS value FROM payments WHERE status = 'successful'",
                        ColSpan: 1),
                    new(
                        Title: "Active Paying Accounts",
                        WidgetType: "metric",
                        Metric: "Customers",
                        Aggregation: "COUNT",
                        SqlQuery: "SELECT COUNT(DISTINCT customer_id) AS value FROM subscriptions WHERE status = 'active'",
                        ColSpan: 1),
                    new(
                        Title: "Average Transaction Value",
                        WidgetType: "metric",
                        Metric: "AOV",
                        Aggregation: "AVG",
                        SqlQuery: "SELECT COALESCE(ROUND(AVG(amount), 2), 245.50) AS value FROM payments",
                        ColSpan: 1),
                    new(
                        Title: "Failed Payment Rate",
                        WidgetType: "metric",
                        Metric: "FailureRate",
                        Aggregation: "AVG",
                        SqlQuery: "SELECT ROUND(COUNT(CASE WHEN status = 'failed' THEN 1 END) * 100.0 / NULLIF(COUNT(*), 0), 2) AS value FROM payments",
                        ColSpan: 1),
                    new(
                        Title: "Daily Revenue Trajectory",
                        WidgetType: "lineChart",
                        Metric: "Revenue",
                        Dimension: "Date",
                        Aggregation: "SUM",
                        SqlQuery: "SELECT DATE_TRUNC('day', created_at) AS date, SUM(amount) AS value FROM payments GROUP BY 1 ORDER BY 1",
                        ColSpan: 2),
                    new(
                        Title: "Revenue by Payment Provider",
                        WidgetType: "pieChart",
                        Metric: "Revenue",
                        Dimension: "Provider",
                        Aggregation: "SUM",
                        SqlQuery: "SELECT provider AS category, SUM(amount) AS value FROM payments GROUP BY 1 ORDER BY 2 DESC",
                        ColSpan: 2),
                    new(
                        Title: "Top Revenue Generating Accounts",
                        WidgetType: "table",
                        Metric: "Revenue",
                        Dimension: "Account",
                        Aggregation: "SUM",
                        SqlQuery: "SELECT customer_id AS customer, SUM(amount) AS total_revenue, COUNT(*) AS transactions FROM payments GROUP BY 1 ORDER BY 2 DESC LIMIT 10",
                        ColSpan: 4)
                }), JsonOptions);
        }

        if (prompt.Contains("customer") || prompt.Contains("churn") || prompt.Contains("retention") || prompt.Contains("user"))
        {
            return JsonSerializer.Serialize(new DashboardAgentResult(
                Title: "Customer Retention & Churn Analytics",
                Description: "In-depth breakdown of customer lifecycle metrics, churn hazard rates, and account tier movements.",
                DateRange: "Last 30 Days",
                Widgets: new List<DashboardAgentWidget>
                {
                    new(
                        Title: "Total Active Customers",
                        WidgetType: "metric",
                        Metric: "ActiveCustomers",
                        Aggregation: "COUNT",
                        SqlQuery: "SELECT COUNT(*) AS value FROM customers WHERE status = 'active'",
                        ColSpan: 1),
                    new(
                        Title: "Churn Rate (Trailing 30D)",
                        WidgetType: "metric",
                        Metric: "ChurnRate",
                        Aggregation: "AVG",
                        SqlQuery: "SELECT 2.4 AS value",
                        ColSpan: 1),
                    new(
                        Title: "Net Retention Rate (NRR)",
                        WidgetType: "metric",
                        Metric: "NRR",
                        Aggregation: "AVG",
                        SqlQuery: "SELECT 112.5 AS value",
                        ColSpan: 1),
                    new(
                        Title: "New Signups MTD",
                        WidgetType: "metric",
                        Metric: "Signups",
                        Aggregation: "COUNT",
                        SqlQuery: "SELECT COUNT(*) AS value FROM customers WHERE created_at >= DATE_TRUNC('month', CURRENT_DATE)",
                        ColSpan: 1),
                    new(
                        Title: "Customer Growth vs Churn Trend",
                        WidgetType: "areaChart",
                        Metric: "CustomerCount",
                        Dimension: "Week",
                        Aggregation: "COUNT",
                        SqlQuery: "SELECT DATE_TRUNC('week', created_at) AS week, COUNT(*) AS value FROM customers GROUP BY 1 ORDER BY 1",
                        ColSpan: 2),
                    new(
                        Title: "Customer Distribution by Plan Tier",
                        WidgetType: "barChart",
                        Metric: "Accounts",
                        Dimension: "Plan",
                        Aggregation: "COUNT",
                        SqlQuery: "SELECT plan AS tier, COUNT(*) AS accounts FROM subscriptions GROUP BY 1 ORDER BY 2 DESC",
                        ColSpan: 2)
                }), JsonOptions);
        }

        // Generic Executive Overview Fallback
        return JsonSerializer.Serialize(BuildFallbackDashboard("Executive Decision Dashboard", "High-level overview of organizational health and core metrics."), JsonOptions);
    }

    private static DashboardAgentResult BuildFallbackDashboard(string title, string description)
    {
        return new DashboardAgentResult(
            Title: title,
            Description: description,
            DateRange: "Last 30 Days",
            Widgets: new List<DashboardAgentWidget>
            {
                new(
                    Title: "Primary Business Metric",
                    WidgetType: "metric",
                    Metric: "KeyMetric",
                    Aggregation: "SUM",
                    SqlQuery: "SELECT 100 AS value",
                    ColSpan: 1),
                new(
                    Title: "Secondary Operational KPI",
                    WidgetType: "metric",
                    Metric: "OperationalKpi",
                    Aggregation: "AVG",
                    SqlQuery: "SELECT 98.5 AS value",
                    ColSpan: 1),
                new(
                    Title: "System Throughput",
                    WidgetType: "metric",
                    Metric: "Throughput",
                    Aggregation: "COUNT",
                    SqlQuery: "SELECT 1250 AS value",
                    ColSpan: 1),
                new(
                    Title: "Incident Frequency",
                    WidgetType: "metric",
                    Metric: "Incidents",
                    Aggregation: "COUNT",
                    SqlQuery: "SELECT 0 AS value",
                    ColSpan: 1),
                new(
                    Title: "Performance Trajectory",
                    WidgetType: "lineChart",
                    Metric: "Performance",
                    Dimension: "Date",
                    Aggregation: "SUM",
                    SqlQuery: "SELECT CURRENT_DATE - s.i AS date, 100 + s.i * 5 AS value FROM generate_series(0, 14) AS s(i) ORDER BY 1",
                    ColSpan: 2),
                new(
                    Title: "Segment Distribution",
                    WidgetType: "pieChart",
                    Metric: "Volume",
                    Dimension: "Segment",
                    Aggregation: "SUM",
                    SqlQuery: "SELECT 'Tier 1' AS category, 45 AS value UNION ALL SELECT 'Tier 2', 35 UNION ALL SELECT 'Tier 3', 20",
                    ColSpan: 2)
            });
    }
}
