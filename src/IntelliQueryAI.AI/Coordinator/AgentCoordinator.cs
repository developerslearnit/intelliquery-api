using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using IntelliQueryAI.AI.Agents;
using IntelliQueryAI.AI.Embeddings;
using IntelliQueryAI.AI.Evaluations;
using IntelliQueryAI.AI.Memory;
using IntelliQueryAI.AI.Telemetry;
using IntelliQueryAI.AI.Tools;
using IntelliQueryAI.Domain.Entities.Ai;
using IntelliQueryAI.Domain.Entities.Audit;
using IntelliQueryAI.Domain.Entities.DataSources;
using IntelliQueryAI.Domain.Entities.Governance;
using IntelliQueryAI.Domain.Errors;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.Domain.Services;
using IntelliQueryAI.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace IntelliQueryAI.AI.Coordinator;

public sealed record CoordinatorQueryRequest(
    Guid WorkspaceId,
    Guid DataSourceId,
    Guid UserId,
    string Question,
    Guid? SessionId = null,
    string? ContextPrompt = null,
    string? UserRole = null);

public sealed record CoordinatorQueryResponse(
    string Question,
    string GeneratedSql,
    ExecutionQueryResult Results,
    IReadOnlyList<string> Insights,
    ChartConfiguration RecommendedChart,
    double ConfidenceScore,
    long TotalDurationMs,
    int TotalTokensUsed);

public interface IAgentCoordinator
{
    Task<Result<CoordinatorQueryResponse>> ExecuteQueryPipelineAsync(
        CoordinatorQueryRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class AgentCoordinator : IAgentCoordinator
{
    private readonly IntentUnderstandingAgent _intentAgent;
    private readonly SqlGenerationAgent _sqlGenAgent;
    private readonly SqlValidationAgent _sqlValidationAgent;
    private readonly DataAnalystAgent _dataAnalystAgent;
    private readonly VisualizationAgent _visualizationAgent;
    private readonly SchemaRetrievalTools _schemaTools;
    private readonly SqlExecutionTools _sqlExecutionTools;
    private readonly IEmbeddingGenerator _embeddingGenerator;
    private readonly IDataSourceStore _dataSourceStore;
    private readonly IEncryptionService _encryptionService;
    private readonly IQueryHistoryStore _queryHistoryStore;
    private readonly IAgentMemoryService _memoryService;
    private readonly ITelemetryService _telemetry;
    private readonly ILogger<AgentCoordinator> _logger;
    private readonly ISchemaReadStore? _schemaReadStore;
    private readonly ISchemaDiscoveryEngine? _discoveryEngine;
    private readonly ISchemaMetadataStore? _schemaMetadataStore;
    private readonly ISchemaEmbeddingIndexer? _embeddingIndexer;
    private readonly IUnitOfWork? _unitOfWork;
    private readonly IBusinessTermStore? _businessTermStore;
    private readonly IMetricStore? _metricStore;
    private readonly IGovernancePolicyStore? _governanceStore;
    private readonly IDataMaskingEngine? _maskingEngine;
    private readonly IAuditLogStore? _auditLogStore;

    public AgentCoordinator(
        IntentUnderstandingAgent intentAgent,
        SqlGenerationAgent sqlGenAgent,
        SqlValidationAgent sqlValidationAgent,
        DataAnalystAgent dataAnalystAgent,
        VisualizationAgent visualizationAgent,
        SchemaRetrievalTools schemaTools,
        SqlExecutionTools sqlExecutionTools,
        IEmbeddingGenerator embeddingGenerator,
        IDataSourceStore dataSourceStore,
        IEncryptionService encryptionService,
        IQueryHistoryStore queryHistoryStore,
        IAgentMemoryService memoryService,
        ITelemetryService telemetry,
        ILogger<AgentCoordinator> logger,
        ISchemaReadStore? schemaReadStore = null,
        ISchemaDiscoveryEngine? discoveryEngine = null,
        ISchemaMetadataStore? schemaMetadataStore = null,
        ISchemaEmbeddingIndexer? embeddingIndexer = null,
        IUnitOfWork? unitOfWork = null,
        IBusinessTermStore? businessTermStore = null,
        IMetricStore? metricStore = null,
        IGovernancePolicyStore? governanceStore = null,
        IDataMaskingEngine? maskingEngine = null,
        IAuditLogStore? auditLogStore = null)
    {
        _intentAgent = intentAgent;
        _sqlGenAgent = sqlGenAgent;
        _sqlValidationAgent = sqlValidationAgent;
        _dataAnalystAgent = dataAnalystAgent;
        _visualizationAgent = visualizationAgent;
        _schemaTools = schemaTools;
        _sqlExecutionTools = sqlExecutionTools;
        _embeddingGenerator = embeddingGenerator;
        _dataSourceStore = dataSourceStore;
        _encryptionService = encryptionService;
        _queryHistoryStore = queryHistoryStore;
        _memoryService = memoryService;
        _telemetry = telemetry;
        _logger = logger;
        _schemaReadStore = schemaReadStore;
        _discoveryEngine = discoveryEngine;
        _schemaMetadataStore = schemaMetadataStore;
        _embeddingIndexer = embeddingIndexer;
        _unitOfWork = unitOfWork;
        _businessTermStore = businessTermStore;
        _metricStore = metricStore;
        _governanceStore = governanceStore;
        _maskingEngine = maskingEngine;
        _auditLogStore = auditLogStore;
    }

    public async Task<Result<CoordinatorQueryResponse>> ExecuteQueryPipelineAsync(
        CoordinatorQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var totalStopwatch = Stopwatch.StartNew();
        int totalTokens = 0;

        _logger.LogInformation("Starting AI agent orchestration for question: '{Question}'", request.Question);

        // 1. Fetch and validate target data source
        var dataSource = await _dataSourceStore.GetByIdAsync(request.DataSourceId, cancellationToken);
        if (dataSource is null || dataSource.WorkspaceId != request.WorkspaceId)
        {
            return Result.Failure<CoordinatorQueryResponse>(DataSourceErrors.NotFound);
        }

        if (!dataSource.IsActive)
        {
            return Result.Failure<CoordinatorQueryResponse>(DataSourceErrors.Inactive);
        }

        // Just-in-time schema discovery: if no schema metadata exists for this data source, automatically discover and index it
        if (_schemaReadStore != null)
        {
            var existingTables = await _schemaReadStore.GetTablesAsync(request.WorkspaceId, request.DataSourceId, cancellationToken);
            if (existingTables.Count == 0 && _discoveryEngine != null && _schemaMetadataStore != null)
            {
                _logger.LogInformation("No schema cataloged for data source '{Name}'. Triggering automated just-in-time discovery...", dataSource.Name);
                try
                {
                    string rawConn = _encryptionService.Decrypt(dataSource.EncryptedConnectionString);
                    var discovery = await _discoveryEngine.DiscoverSchemaAsync(
                        request.WorkspaceId,
                        request.DataSourceId,
                        dataSource.DatabaseType,
                        rawConn,
                        cancellationToken);

                    if (discovery.Columns.Count > 0)
                    {
                        await _schemaMetadataStore.ReplaceSchemaAsync(
                            request.WorkspaceId,
                            request.DataSourceId,
                            discovery.Columns,
                            discovery.Relationships,
                            discovery.Routines,
                            cancellationToken);

                        if (_embeddingIndexer != null)
                        {
                            await _embeddingIndexer.IndexSchemaAsync(
                                request.WorkspaceId,
                                request.DataSourceId,
                                discovery.Columns,
                                discovery.Relationships,
                                cancellationToken);
                        }

                        dataSource.MarkSyncCompleted();
                        if (_unitOfWork != null)
                        {
                            await _unitOfWork.SaveChangesAsync(cancellationToken);
                        }

                        existingTables = await _schemaReadStore.GetTablesAsync(request.WorkspaceId, request.DataSourceId, cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Automated just-in-time schema discovery failed for data source {Id}", dataSource.Id);
                }
            }

            if (existingTables.Count == 0)
            {
                return Result.Failure<CoordinatorQueryResponse>(AiErrors.NoSchemaCataloged(dataSource.Name));
            }
        }

        // 2. Stage 1: Intent Understanding Agent
        var intentResponse = await _intentAgent.ExecuteAsync(new AgentRequest(request.Question), cancellationToken);
        totalTokens += intentResponse.TotalTokens;

        // 3. Stage 2: Hybrid Vector Search & Schema Retrieval (using question keywords + vector similarity)
        var questionEmbedding = await _embeddingGenerator.GenerateEmbeddingAsync(request.Question, cancellationToken);
        var schemaContext = await _schemaTools.RetrieveRelevantSchemaAsync(
            request.WorkspaceId,
            request.DataSourceId,
            request.Question,
            questionEmbedding,
            topK: 15,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(schemaContext.FormattedText))
        {
            return Result.Failure<CoordinatorQueryResponse>(AiErrors.NoSchemaCataloged(dataSource.Name));
        }

        // 3b. Retrieve relevant Business Knowledge and Metric Definitions (Semantic Layer)
        string semanticKnowledgeContext = string.Empty;
        if (_businessTermStore != null || _metricStore != null)
        {
            var tokens = ExtractKeywords(request.Question);
            var relevantTerms = _businessTermStore != null
                ? await _businessTermStore.SearchRelevantTermsAsync(request.WorkspaceId, tokens, cancellationToken)
                : Array.Empty<IntelliQueryAI.Domain.Entities.BusinessKnowledge.BusinessTerm>();
            var relevantMetrics = _metricStore != null
                ? await _metricStore.SearchRelevantMetricsAsync(request.WorkspaceId, tokens, cancellationToken)
                : Array.Empty<IntelliQueryAI.Domain.Entities.BusinessKnowledge.MetricDefinition>();

            if (relevantTerms.Count > 0 || relevantMetrics.Count > 0)
            {
                var sb = new StringBuilder();
                sb.AppendLine("\nBusiness Knowledge & Semantic Definitions (Must be respected in SQL calculation):");
                foreach (var term in relevantTerms)
                {
                    sb.Append($"- Term '{term.Name}': {term.Definition}");
                    if (!string.IsNullOrWhiteSpace(term.TargetTable)) sb.Append($" [Table: {term.TargetTable}]");
                    if (!string.IsNullOrWhiteSpace(term.TargetColumn)) sb.Append($" [Column: {term.TargetColumn}]");
                    if (!string.IsNullOrWhiteSpace(term.SqlCalculationRule)) sb.Append($" [Rule: {term.SqlCalculationRule}]");
                    sb.AppendLine();
                }
                foreach (var metric in relevantMetrics)
                {
                    sb.Append($"- Metric '{metric.Name}' ({metric.AggregationType}): {metric.Description}. Formula: {metric.CalculationFormula}");
                    if (!string.IsNullOrWhiteSpace(metric.FilterExpression)) sb.Append($" [Filter: {metric.FilterExpression}]");
                    if (!string.IsNullOrWhiteSpace(metric.TargetTable)) sb.Append($" [Table: {metric.TargetTable}]");
                    sb.AppendLine();
                }
                semanticKnowledgeContext = sb.ToString();
            }
        }

        // 3c. Contextual Intelligence Target (if provided from application context)
        if (!string.IsNullOrWhiteSpace(request.ContextPrompt))
        {
            semanticKnowledgeContext += $"\nContextual Intelligence Target (Focus analysis on this entity/metric):\n{request.ContextPrompt}\n";
        }

        // 4. Stage 3: SQL Generation Agent
        string sqlGenPrompt = $"""
            User Question: {request.Question}
            Target Database Dialect: {dataSource.DatabaseType}
            Extracted Intent: {intentResponse.Content}
            {semanticKnowledgeContext}
            Relevant Database Schema:
            {schemaContext.FormattedText}
            """;

        var sqlGenResponse = await _sqlGenAgent.ExecuteAsync(new AgentRequest(sqlGenPrompt), cancellationToken);
        totalTokens += sqlGenResponse.TotalTokens;

        // 5. Stage 4: SQL Validation Agent (Security Gatekeeper)
        var validation = _sqlValidationAgent.Validate(sqlGenResponse.Content, dataSource.DatabaseType);
        if (!validation.IsAllowed)
        {
            _logger.LogWarning("SQL Validation rejected query: {Reasons}", string.Join(", ", validation.BlockedReasons));
            bool isMissingEntity = validation.BlockedReasons.Any(r => r.Contains("explanatory message", StringComparison.OrdinalIgnoreCase));
            var error = isMissingEntity
                ? AiErrors.TableNotFoundInSchema(string.Join("; ", validation.BlockedReasons))
                : AiErrors.UnsafeSqlDetected(string.Join("; ", validation.BlockedReasons));

            return Result.Failure<CoordinatorQueryResponse>(error);
        }

        // 6. Stage 5: Safe Query Execution against Data Source
        string connectionString = _encryptionService.Decrypt(dataSource.EncryptedConnectionString);
        var executionResult = await _sqlExecutionTools.ExecuteSafeQueryAsync(
            dataSource.DatabaseType,
            connectionString,
            validation.SanitizedSql,
            timeoutSeconds: 20,
            cancellationToken);

        // Enterprise Data Governance & AI Leakage Prevention (Phase F)
        IReadOnlyList<GovernancePolicy> activePolicies = Array.Empty<GovernancePolicy>();
        if (_governanceStore != null)
        {
            activePolicies = await _governanceStore.GetByWorkspaceIdAsync(request.WorkspaceId, activeOnly: true, cancellationToken);
        }

        string userRole = !string.IsNullOrWhiteSpace(request.UserRole) ? request.UserRole : "Member";

        // Pre-mask sample rows before entering LLM Prompt Context
        var aiSampleRows = executionResult.Rows.Take(5).ToList();
        if (_maskingEngine != null && activePolicies.Count > 0)
        {
            aiSampleRows = _maskingEngine.MaskRows(aiSampleRows, tableName: null, activePolicies, userRole: userRole, isAiContext: true).ToList();
        }

        // 7. Stage 6: Data Analyst Agent (Insights & Summaries)
        string analysisPrompt = $"""
            Original Question: {request.Question}
            {(!string.IsNullOrWhiteSpace(request.ContextPrompt) ? $"Application Context: {request.ContextPrompt}\n" : "")}Generated SQL: {validation.SanitizedSql}
            Row Count: {executionResult.RowCount}
            Columns: {string.Join(", ", executionResult.Columns)}
            Data Sample (First 5 rows):
            {JsonSerializer.Serialize(aiSampleRows)}
            """;

        var analystResponse = await _dataAnalystAgent.ExecuteAsync(new AgentRequest(analysisPrompt), cancellationToken);
        totalTokens += analystResponse.TotalTokens;

        IReadOnlyList<string> insights;
        string cleanAnalystJson = StripMarkdownFences(analystResponse.Content);
        try
        {
            var parsedAnalysis = JsonSerializer.Deserialize<AnalysisOutput>(cleanAnalystJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (parsedAnalysis != null)
            {
                var list = new List<string>();
                if (!string.IsNullOrWhiteSpace(parsedAnalysis.Summary))
                    list.Add(parsedAnalysis.Summary);
                if (parsedAnalysis.KeyInsights?.Count > 0)
                    list.AddRange(parsedAnalysis.KeyInsights);
                if (parsedAnalysis.Outliers?.Count > 0)
                    list.AddRange(parsedAnalysis.Outliers.Select(o => $"Outlier: {o}"));
                if (parsedAnalysis.Recommendations?.Count > 0)
                    list.AddRange(parsedAnalysis.Recommendations.Select(r => $"Recommendation: {r}"));
                insights = list.Count > 0 ? list : [analystResponse.Content];
            }
            else
            {
                insights = [analystResponse.Content];
            }
        }
        catch
        {
            insights = [analystResponse.Content];
        }

        // 8. Stage 7: Visualization Recommendation Agent
        var vizSampleRows = executionResult.Rows.Take(3).ToList();
        if (_maskingEngine != null && activePolicies.Count > 0)
        {
            vizSampleRows = _maskingEngine.MaskRows(vizSampleRows, tableName: null, activePolicies, userRole: userRole, isAiContext: true).ToList();
        }

        string sampleRowsJson = vizSampleRows.Count > 0
            ? JsonSerializer.Serialize(vizSampleRows)
            : "[]";

        string vizPrompt = $"""
            Analytical Question: {request.Question}
            Result Columns: {string.Join(", ", executionResult.Columns)}
            Row Count: {executionResult.RowCount}
            Sample Data (first 3 rows):
            {sampleRowsJson}
            """;

        var vizResponse = await _visualizationAgent.ExecuteAsync(new AgentRequest(vizPrompt), cancellationToken);
        totalTokens += vizResponse.TotalTokens;

        ChartConfiguration chartConfig = ParseOrDetermineChartConfig(
            vizResponse.Content,
            executionResult,
            request.Question);

        totalStopwatch.Stop();

        // 9. Mask query results for user presentation and record sensitive access audit
        ExecutionQueryResult finalResult = executionResult;
        if (_maskingEngine != null && activePolicies.Count > 0)
        {
            var maskedUserRows = _maskingEngine.MaskRows(executionResult.Rows, tableName: null, activePolicies, userRole: userRole, isAiContext: false);
            finalResult = executionResult with { Rows = maskedUserRows };

            if (_auditLogStore != null)
            {
                var matchedPolicies = activePolicies.Where(p => executionResult.Columns.Any(c => p.Matches(null, c))).ToList();
                if (matchedPolicies.Count > 0)
                {
                    var auditLog = AuditLog.Create(
                        workspaceId: request.WorkspaceId,
                        userId: request.UserId,
                        action: "Governance.SensitiveDataQuery",
                        entityType: "GovernancePolicy",
                        entityId: matchedPolicies[0].Id.ToString(),
                        details: $"User with role '{userRole}' accessed query results governed by {matchedPolicies.Count} policies: {string.Join(", ", matchedPolicies.Select(p => p.TargetColumn))}");
                    await _auditLogStore.AddAsync(auditLog, cancellationToken);
                }
            }
        }

        // 10. Persist Query History & Audit Trail
        var history = QueryHistory.Create(
            request.WorkspaceId,
            request.DataSourceId,
            request.UserId,
            request.SessionId,
            request.Question,
            validation.SanitizedSql,
            executionResult.ExecutionDurationMs,
            executionResult.RowCount,
            executionResult.IsSuccess,
            executionResult.ErrorMessage,
            validation.ConfidenceScore);

        await _queryHistoryStore.AddAsync(history, cancellationToken);

        // 11. Update multi-turn memory session if applicable
        if (request.SessionId.HasValue)
        {
            _memoryService.AppendMessage(request.SessionId.Value, "User", request.Question);
            _memoryService.AppendMessage(request.SessionId.Value, "Assistant", $"Executed SQL: {validation.SanitizedSql}. Found {executionResult.RowCount} rows.");
        }

        var response = new CoordinatorQueryResponse(
            Question: request.Question,
            GeneratedSql: validation.SanitizedSql,
            Results: finalResult,
            Insights: insights,
            RecommendedChart: chartConfig,
            ConfidenceScore: validation.ConfidenceScore,
            TotalDurationMs: totalStopwatch.ElapsedMilliseconds,
            TotalTokensUsed: totalTokens);

        return Result.Success(response);
    }

    private static ChartConfiguration ParseOrDetermineChartConfig(
        string rawContent,
        ExecutionQueryResult executionResult,
        string question)
    {
        string cleanJson = StripMarkdownFences(rawContent);
        ChartConfiguration? parsed = null;
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
            };
            parsed = JsonSerializer.Deserialize<ChartConfiguration>(cleanJson, options);
        }
        catch
        {
            // fallback handled below
        }

        // Validate parsed config: ensure X-axis is not empty, is an actual column, and is NOT a GUID/ID
        bool isValidX = parsed != null
            && !string.IsNullOrWhiteSpace(parsed.XAxisColumn)
            && executionResult.Columns.Contains(parsed.XAxisColumn, StringComparer.OrdinalIgnoreCase)
            && !IsIdentifierColumn(parsed.XAxisColumn, executionResult.Rows);

        if (isValidX && parsed != null)
        {
            return parsed;
        }

        return DetermineSmartChartFallback(executionResult, question);
    }

    private static bool IsIdentifierColumn(string colName, IReadOnlyList<IReadOnlyDictionary<string, object?>> rows)
    {
        string lower = colName.ToLowerInvariant();
        if (lower == "id" || lower.EndsWith("_id") || lower.EndsWith("id") || lower.Contains("guid") || lower.Contains("uuid"))
        {
            if (rows.Count > 0 && rows[0].TryGetValue(colName, out var val) && val is not null)
            {
                string strVal = val.ToString() ?? "";
                if (Guid.TryParse(strVal, out _) || Regex.IsMatch(strVal, @"^[0-9a-fA-F\-]{32,36}$"))
                {
                    return true;
                }
            }
            if (lower == "id" || lower.EndsWith("_id") || lower == "guid" || lower == "uuid")
            {
                return true;
            }
        }
        return false;
    }

    private static ChartConfiguration DetermineSmartChartFallback(ExecutionQueryResult result, string question)
    {
        var nonIdCols = result.Columns
            .Where(c => !IsIdentifierColumn(c, result.Rows))
            .ToList();

        // 1. Find best dimension column
        string xCol = nonIdCols.FirstOrDefault(c =>
        {
            string l = c.ToLowerInvariant();
            return l.Contains("name") || l.Contains("title") || l.Contains("status") ||
                   l.Contains("department") || l.Contains("category") || l.Contains("type") ||
                   l.Contains("date") || l.Contains("month") || l.Contains("year") || l.Contains("role");
        }) ?? nonIdCols.FirstOrDefault() ?? result.Columns.FirstOrDefault() ?? "Dimension";

        // 2. Find numeric columns
        var numericCols = nonIdCols.Where(c =>
        {
            if (result.Rows.Count == 0) return false;
            var val = result.Rows[0].TryGetValue(c, out var v) ? v : null;
            if (val is null) return false;
            return val is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal;
        }).ToList();

        if (numericCols.Count > 0)
        {
            var yCols = numericCols.Take(3).ToList();
            bool isTemporal = xCol.ToLowerInvariant().Contains("date") || xCol.ToLowerInvariant().Contains("month") || xCol.ToLowerInvariant().Contains("year");
            return new ChartConfiguration(
                RecommendedChart: isTemporal ? Domain.Enums.ChartType.LineChart : Domain.Enums.ChartType.BarChart,
                Title: $"{string.Join(" & ", yCols)} by {xCol}",
                XAxisColumn: xCol,
                YAxisColumns: yCols,
                Description: $"Visualizing {string.Join(", ", yCols)} across {xCol}.",
                StylingOptions: null);
        }

        // If NO numeric columns exist (e.g. employee roster) -> frequency/distribution breakdown!
        return new ChartConfiguration(
            RecommendedChart: Domain.Enums.ChartType.BarChart,
            Title: $"Record Distribution by {xCol}",
            XAxisColumn: xCol,
            YAxisColumns: ["Count"],
            Description: $"Frequency distribution of {result.RowCount} records categorized by {xCol}.",
            StylingOptions: null);
    }

    private static string StripMarkdownFences(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return string.Empty;
        string trimmed = content.Trim();
        if (trimmed.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed[7..];
        else if (trimmed.StartsWith("```"))
            trimmed = trimmed[3..];

        if (trimmed.EndsWith("```"))
            trimmed = trimmed[..^3];

        return trimmed.Trim();
    }

    private static List<string> ExtractKeywords(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt)) return new List<string>();
        var cleaned = System.Text.RegularExpressions.Regex.Replace(prompt, @"[^\w\s]", " ");
        return cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 3)
            .Select(w => w.ToLowerInvariant())
            .Distinct()
            .ToList();
    }
}
