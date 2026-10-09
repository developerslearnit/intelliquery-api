using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using IntelliQueryAI.AI.Common;
using IntelliQueryAI.AI.Evaluations;
using IntelliQueryAI.AI.Telemetry;
using IntelliQueryAI.Domain.Enums;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IntelliQueryAI.AI.Agents;

// ---------------------------------------------------------------------------
// 1. Schema Discovery Agent
// ---------------------------------------------------------------------------
public sealed class SchemaDiscoveryAgent : BaseAgent
{
    public SchemaDiscoveryAgent(
        IChatClient? chatClient,
        ITelemetryService telemetry,
        ILogger<SchemaDiscoveryAgent> logger,
        IOptions<AzureOpenAISettings> options,
        SimulationMode simulationMode)
        : base(
            chatClient,
            instructions: """
                You are the Schema Discovery Agent for the IntelliQueryAI platform.
                Your role is to analyze technical relational schemas, understand column data types and foreign key relationships,
                and generate rich, searchable semantic descriptions and business terms for hybrid vector storage in PgVector.
                Always identify the primary business purpose of tables and provide clear semantic mappings.
                """,
            name: "SchemaDiscoveryAgent",
            description: "Catalogs schema tables, columns, and relationships into semantic vector embeddings.",
            tools: null,
            telemetry: telemetry,
            logger: logger,
            simulationMode: simulationMode,
            temperature: options?.Value?.Temperature)
    {
    }

    protected override string GenerateSimulatedResponse(AgentRequest request)
    {
        return """
            {
                "summary": "Cataloged schema successfully.",
                "semanticTerms": ["Customer", "Orders", "Invoices", "Products", "Subscriptions"],
                "embeddingTokens": 256
            }
            """;
    }
}

// ---------------------------------------------------------------------------
// 2. Intent Understanding Agent
// ---------------------------------------------------------------------------
public sealed record ParsedUserIntent(
    QueryIntent Intent,
    string PrimaryEntity,
    IReadOnlyList<string> Metrics,
    IReadOnlyList<string> Dimensions,
    IReadOnlyList<string> Filters,
    string? TimeRange,
    int? Limit);

public sealed class IntentUnderstandingAgent : BaseAgent
{
    public IntentUnderstandingAgent(
        IChatClient? chatClient,
        ITelemetryService telemetry,
        ILogger<IntentUnderstandingAgent> logger,
        IOptions<AzureOpenAISettings> options,
        SimulationMode simulationMode)
        : base(
            chatClient,
            instructions: """
                You are the Intent Understanding Agent for IntelliQueryAI.
                Your task is to analyze the user's natural language question and extract precise analytical intent.
                Classify the intent into: Aggregation, Filtering, Trend, Lookup, Comparison, AnomalyDetection, CohortAnalysis, or Custom.
                Extract:
                1. Primary target entity (e.g. customers, orders, revenue, products)
                2. Metrics (e.g. sum of amount, count of users, average price)
                3. Dimensions (e.g. country, month, status, product category)
                4. Filters (e.g. status = 'active', country = 'US')
                5. Temporal constraints (e.g. 'last month', 'year to date')
                6. Limit / Top count (e.g. top 10)
                Output strictly JSON adhering to the ParsedUserIntent structure.
                """,
            name: "IntentUnderstandingAgent",
            description: "Extracts analytical intent, entities, metrics, dimensions, and temporal filters from natural language prompts.",
            tools: null,
            telemetry: telemetry,
            logger: logger,
            simulationMode: simulationMode,
            temperature: options?.Value?.Temperature)
    {
    }

    protected override string GenerateSimulatedResponse(AgentRequest request)
    {
        string prompt = request.Prompt.ToLowerInvariant();
        var intent = QueryIntent.Aggregation;
        if (prompt.Contains("trend") || prompt.Contains("over time") || prompt.Contains("monthly"))
        {
            intent = QueryIntent.Trend;
        }
        else if (prompt.Contains("compare") || prompt.Contains("vs"))
        {
            intent = QueryIntent.Comparison;
        }
        else if (prompt.Contains("find") || prompt.Contains("lookup") || prompt.Contains("details"))
        {
            intent = QueryIntent.Lookup;
        }

        var parsed = new ParsedUserIntent(
            Intent: intent,
            PrimaryEntity: "CustomerOrders",
            Metrics: ["TotalAmount", "OrderCount"],
            Dimensions: ["CustomerName", "OrderDate"],
            Filters: ["IsActive = true"],
            TimeRange: "Last30Days",
            Limit: 10);

        return JsonSerializer.Serialize(parsed);
    }
}

// ---------------------------------------------------------------------------
// 3. SQL Generation Agent
// ---------------------------------------------------------------------------
public sealed class SqlGenerationAgent : BaseAgent
{
    public SqlGenerationAgent(
        IChatClient? chatClient,
        ITelemetryService telemetry,
        ILogger<SqlGenerationAgent> logger,
        IOptions<AzureOpenAISettings> options,
        SimulationMode simulationMode)
        : base(
            chatClient,
            instructions: """
                You are the SQL Generation Agent for IntelliQueryAI.
                You receive user analytical intent along with the relevant schema context retrieved from the database catalog.
                Rules:
                1. Generate ONLY valid, read-only SELECT SQL statements matching the specified database dialect.
                2. Use clear table aliases and explicit JOIN ON clauses using foreign key relationships.
                3. Apply GROUP BY, ORDER BY, and WHERE clauses accurately matching the user question.
                4. Always use the exact table and column names provided in the schema context. If a schema prefix is provided (e.g. dbo.table or ebs.table), qualify the table name accordingly.
                5. Map user business terminology to the closest matching table and columns in the provided schema (e.g., if the user asks for 'ebs transactions', look for tables like 'ebs_transactions', 'transactions', or tables under an 'ebs' schema).
                6. NEVER return conversational messages, explanations, or dummy SELECT queries returning text like "The provided schema does not include...". You must generate an actual SQL query against the cataloged schema.
                7. Never generate DDL or DML statements (DELETE, UPDATE, INSERT, DROP, TRUNCATE, ALTER).
                8. Always add safety row limits (LIMIT 50 for PostgreSQL/MySQL, TOP 50 for SQL Server) if not specified by the user's question (e.g. if the user asks for top 20, use TOP 20 / LIMIT 20).
                9. Output only the executable SQL statement inside a sql code block.
                """,
            name: "SqlGenerationAgent",
            description: "Formulates dialect-specific, optimized read-only SELECT SQL queries from relevant schema context.",
            tools: null,
            telemetry: telemetry,
            logger: logger,
            simulationMode: simulationMode,
            temperature: options?.Value?.Temperature)
    {
    }

    protected override string GenerateSimulatedResponse(AgentRequest request)
    {
        string prompt = request.Prompt.ToLowerInvariant();
        if (prompt.Contains("customer") || prompt.Contains("user"))
        {
            return """
                SELECT 
                    c.id AS customer_id,
                    c.name AS customer_name,
                    COUNT(o.id) AS total_orders,
                    COALESCE(SUM(o.total_amount), 0) AS total_revenue
                FROM customers c
                LEFT JOIN orders o ON c.id = o.customer_id
                WHERE c.is_active = true
                GROUP BY c.id, c.name
                ORDER BY total_revenue DESC
                LIMIT 10;
                """;
        }

        return """
            SELECT 
                DATE_TRUNC('month', created_at) AS order_month,
                COUNT(id) AS total_orders,
                SUM(total_amount) AS revenue
            FROM orders
            GROUP BY DATE_TRUNC('month', created_at)
            ORDER BY order_month DESC
            LIMIT 12;
            """;
    }
}

// ---------------------------------------------------------------------------
// 4. SQL Validation Agent (Security Gatekeeper)
// ---------------------------------------------------------------------------
public sealed record SqlValidationOutput(
    bool IsAllowed,
    string SanitizedSql,
    double ConfidenceScore,
    IReadOnlyList<string> BlockedReasons,
    long EstimatedCostScore);

public sealed class SqlValidationAgent : BaseAgent
{
    private readonly IAgentEvaluationService _evaluationService;

    public SqlValidationAgent(
        IChatClient? chatClient,
        IAgentEvaluationService evaluationService,
        ITelemetryService telemetry,
        ILogger<SqlValidationAgent> logger,
        IOptions<AzureOpenAISettings> options,
        SimulationMode simulationMode)
        : base(
            chatClient,
            instructions: """
                You are the SQL Validation Agent and Security Gatekeeper for IntelliQueryAI.
                Your duty is to strictly verify the security and integrity of generated SQL queries before execution.
                Prohibit all destructive or mutating statements (DELETE, UPDATE, INSERT, DROP, TRUNCATE, ALTER, EXEC).
                Verify query structure, column references, and ensure a mandatory row limit is in place.
                """,
            name: "SqlValidationAgent",
            description: "Validates SQL syntax, security guardrails, blocks mutating commands, and enforces row limits.",
            tools: null,
            telemetry: telemetry,
            logger: logger,
            simulationMode: simulationMode,
            temperature: options?.Value?.Temperature)
    {
        _evaluationService = evaluationService;
    }

    public SqlValidationOutput Validate(string rawSql, DatabaseType dbType)
    {
        string cleanedSql = StripMarkdownCodeBlocks(rawSql).Trim();

        // 1. Evaluate using security rules
        var eval = _evaluationService.EvaluateSql(cleanedSql, Array.Empty<string>(), Array.Empty<string>());
        if (!eval.IsValid)
        {
            return new SqlValidationOutput(
                IsAllowed: false,
                SanitizedSql: string.Empty,
                ConfidenceScore: 0.0,
                BlockedReasons: eval.Issues,
                EstimatedCostScore: 9999);
        }

        // 2. Enforce row limit if absent
        string sanitized = EnsureRowLimit(cleanedSql, dbType);

        // 3. Simple heuristic cost estimation (joins + aggregates)
        long cost = 10;
        int joinCount = Regex.Matches(sanitized, @"\bJOIN\b", RegexOptions.IgnoreCase).Count;
        cost += joinCount * 25;
        if (Regex.IsMatch(sanitized, @"\bGROUP\s+BY\b", RegexOptions.IgnoreCase)) cost += 20;
        if (Regex.IsMatch(sanitized, @"\bORDER\s+BY\b", RegexOptions.IgnoreCase)) cost += 15;

        return new SqlValidationOutput(
            IsAllowed: true,
            SanitizedSql: sanitized,
            ConfidenceScore: eval.ConfidenceScore,
            BlockedReasons: Array.Empty<string>(),
            EstimatedCostScore: cost);
    }

    protected override string GenerateSimulatedResponse(AgentRequest request)
    {
        var output = Validate(request.Prompt, DatabaseType.PostgreSQL);
        return JsonSerializer.Serialize(output);
    }

    private static string StripMarkdownCodeBlocks(string sql)
    {
        string result = Regex.Replace(sql, @"^```(sql)?\s*", "", RegexOptions.IgnoreCase | RegexOptions.Multiline);
        result = Regex.Replace(result, @"\s*```$", "", RegexOptions.Multiline);
        return result.Trim();
    }

    private static string EnsureRowLimit(string sql, DatabaseType dbType)
    {
        bool hasLimit = Regex.IsMatch(sql, @"\b(LIMIT|TOP|FETCH\s+FIRST)\b", RegexOptions.IgnoreCase);
        if (hasLimit)
        {
            return sql;
        }

        // Remove trailing semicolon if present
        string trimmed = sql.TrimEnd(';', ' ', '\r', '\n');

        return dbType switch
        {
            DatabaseType.SqlServer => Regex.Replace(trimmed, @"^(SELECT)\b", "SELECT TOP 1000", RegexOptions.IgnoreCase),
            DatabaseType.MySql => $"{trimmed} LIMIT 1000;",
            _ => $"{trimmed} LIMIT 1000;"
        };
    }
}

// ---------------------------------------------------------------------------
// 5. Data Analyst Agent
// ---------------------------------------------------------------------------
public sealed record AnalysisOutput(
    string Summary,
    IReadOnlyList<string> KeyInsights,
    IReadOnlyList<string> Outliers,
    IReadOnlyList<string> Recommendations);

public sealed class DataAnalystAgent : BaseAgent
{
    public DataAnalystAgent(
        IChatClient? chatClient,
        ITelemetryService telemetry,
        ILogger<DataAnalystAgent> logger,
        IOptions<AzureOpenAISettings> options,
        SimulationMode simulationMode)
        : base(
            chatClient,
            instructions: """
                You are the Data Analyst Agent for IntelliQueryAI.
                Your task is to examine tabular query execution results and generate sharp, executive-level data insights.
                Identify:
                1. Core trends and summary totals
                2. Statistical outliers or notable concentrations
                3. Business takeaways and actionable recommendations
                Keep the tone professional, objective, and data-driven.
                """,
            name: "DataAnalystAgent",
            description: "Extracts statistical patterns, business summaries, and actionable insights from query results.",
            tools: null,
            telemetry: telemetry,
            logger: logger,
            simulationMode: simulationMode,
            temperature: options?.Value?.Temperature)
    {
    }

    protected override string GenerateSimulatedResponse(AgentRequest request)
    {
        var output = new AnalysisOutput(
            Summary: "The top 10 customers represent 68% of total revenue for the analyzed period.",
            KeyInsights:
            [
                "Strong concentration of sales within the top 3 enterprise accounts.",
                "Customer order frequency increased by 14% month-over-month.",
                "Average order value was highest among clients active for over 6 months."
            ],
            Outliers:
            [
                "One account generated 3x higher volume than the second-ranked customer."
            ],
            Recommendations:
            [
                "Implement proactive loyalty outreach to the top tier.",
                "Investigate cross-selling opportunities for mid-tier accounts."
            ]);

        return JsonSerializer.Serialize(output);
    }
}

// ---------------------------------------------------------------------------
// 6. Visualization Recommendation Agent
// ---------------------------------------------------------------------------
public sealed record ChartConfiguration(
    [property: JsonConverter(typeof(JsonStringEnumConverter))]
    ChartType RecommendedChart,
    string Title,
    string XAxisColumn,
    IReadOnlyList<string> YAxisColumns,
    string Description,
    IReadOnlyDictionary<string, string>? StylingOptions);

public sealed class VisualizationAgent : BaseAgent
{
    public VisualizationAgent(
        IChatClient? chatClient,
        ITelemetryService telemetry,
        ILogger<VisualizationAgent> logger,
        IOptions<AzureOpenAISettings> options,
        SimulationMode simulationMode)
        : base(
            chatClient,
            instructions: """
                You are the Visualization Recommendation Agent for IntelliQueryAI.
                Analyze the schema of the query result set (column names and data types), sample rows, and analytical question.
                Recommend the optimal chart type: BarChart, LineChart, AreaChart, PieChart, ScatterPlot, DataTable, MetricCard, or HeatMap.

                CRITICAL RULES:
                1. NEVER select primary keys, foreign keys, or GUID/UUID identifier columns (e.g. 'Id', 'ClientId', 'UserId', 'EmployeeId') as the X-axis.
                2. Select a human-readable categorical column (e.g., name, department, status, title, category) or date/time column for the X-axis.
                3. Select numeric columns (e.g., salary, amount, revenue, total, count) for the Y-axis.
                4. If the result set has NO numeric columns (e.g. employee roster or status list), recommend a BarChart or PieChart grouped by a categorical column representing record frequency/count, or recommend DataTable.
                5. Output strictly valid JSON matching the ChartConfiguration structure, without markdown code block fences.
                """,
            name: "VisualizationAgent",
            description: "Recommends and configures optimal data visualizations (Bar, Line, Area, Pie, MetricCard, etc.).",
            tools: null,
            telemetry: telemetry,
            logger: logger,
            simulationMode: simulationMode,
            temperature: options?.Value?.Temperature)
    {
    }

    protected override string GenerateSimulatedResponse(AgentRequest request)
    {
        string prompt = request.Prompt.ToLowerInvariant();
        ChartType chartType = ChartType.BarChart;
        string xAxis = "customer_name";
        var yAxes = new List<string> { "total_revenue" };

        if (prompt.Contains("trend") || prompt.Contains("month") || prompt.Contains("date"))
        {
            chartType = ChartType.LineChart;
            xAxis = "order_month";
            yAxes = ["revenue"];
        }
        else if (prompt.Contains("distribution") || prompt.Contains("share"))
        {
            chartType = ChartType.PieChart;
            xAxis = "category";
            yAxes = ["percentage"];
        }
        else if (prompt.Contains("total") && !prompt.Contains("by"))
        {
            chartType = ChartType.MetricCard;
            xAxis = "";
            yAxes = ["total_revenue"];
        }

        var config = new ChartConfiguration(
            RecommendedChart: chartType,
            Title: "Analytical Query Visualization",
            XAxisColumn: xAxis,
            YAxisColumns: yAxes,
            Description: $"Recommended {chartType} for presenting multi-dimensional metrics clearly.",
            StylingOptions: new Dictionary<string, string>
            {
                { "colorPalette", "CoolGradients" },
                { "showLegend", "true" },
                { "enableTooltip", "true" }
            });

        return JsonSerializer.Serialize(config);
    }
}
