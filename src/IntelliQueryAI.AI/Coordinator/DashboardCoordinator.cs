using System.Diagnostics;
using IntelliQueryAI.AI.Agents;
using IntelliQueryAI.AI.Common;
using IntelliQueryAI.AI.Telemetry;
using IntelliQueryAI.AI.Tools;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.Domain.Services;
using IntelliQueryAI.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace IntelliQueryAI.AI.Coordinator;

public sealed record GenerateDashboardPromptRequest(
    Guid WorkspaceId,
    string Prompt,
    Guid? DataSourceId = null,
    string? DateRange = null,
    string? WorkspaceName = null);

public sealed record ExecuteWidgetQueryRequest(
    Guid WorkspaceId,
    Guid? DataSourceId,
    string SqlQuery,
    int RowLimit = 100);

public sealed record ExecuteWidgetQueryResponse(
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows,
    long DurationMs,
    int RowCount);

public interface IDashboardCoordinator
{
    Task<Result<DashboardAgentResult>> GenerateDashboardDefinitionAsync(
        GenerateDashboardPromptRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<ExecuteWidgetQueryResponse>> ExecuteWidgetQueryAsync(
        ExecuteWidgetQueryRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class DashboardCoordinator : IDashboardCoordinator
{
    private readonly DashboardGeneratorAgent _agent;
    private readonly IMetricStore _metricStore;
    private readonly IBusinessTermStore _businessTermStore;
    private readonly ISchemaReadStore? _schemaReadStore;
    private readonly IDataSourceStore? _dataSourceStore;
    private readonly IEncryptionService? _encryptionService;
    private readonly SqlExecutionTools? _sqlExecutionTools;
    private readonly IGovernancePolicyStore? _governanceStore;
    private readonly IDataMaskingEngine? _maskingEngine;
    private readonly ITelemetryService _telemetry;
    private readonly ILogger<DashboardCoordinator> _logger;

    public DashboardCoordinator(
        DashboardGeneratorAgent agent,
        IMetricStore metricStore,
        IBusinessTermStore businessTermStore,
        ITelemetryService telemetry,
        ILogger<DashboardCoordinator> logger,
        ISchemaReadStore? schemaReadStore = null,
        IDataSourceStore? dataSourceStore = null,
        IEncryptionService? encryptionService = null,
        SqlExecutionTools? sqlExecutionTools = null,
        IGovernancePolicyStore? governanceStore = null,
        IDataMaskingEngine? maskingEngine = null)
    {
        _agent = agent;
        _metricStore = metricStore;
        _businessTermStore = businessTermStore;
        _schemaReadStore = schemaReadStore;
        _dataSourceStore = dataSourceStore;
        _encryptionService = encryptionService;
        _sqlExecutionTools = sqlExecutionTools;
        _governanceStore = governanceStore;
        _maskingEngine = maskingEngine;
        _telemetry = telemetry;
        _logger = logger;
    }

    public async Task<Result<DashboardAgentResult>> GenerateDashboardDefinitionAsync(
        GenerateDashboardPromptRequest request,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation("Generating AI Dashboard for workspace '{WorkspaceId}' with prompt: '{Prompt}'",
            request.WorkspaceId, request.Prompt);

        // 1. Retrieve Semantic Layer context (Phase A)
        var metrics = await _metricStore.GetByWorkspaceIdAsync(request.WorkspaceId, isActive: true, cancellationToken: cancellationToken);
        var terms = await _businessTermStore.GetByWorkspaceIdAsync(request.WorkspaceId, isActive: true, cancellationToken: cancellationToken);

        string semanticContext = "";
        if (metrics.Count > 0 || terms.Count > 0)
        {
            var metricSnippets = metrics.Take(6).Select(m => $"Metric: '{m.Name}' (Formula: {m.CalculationFormula}, Unit: {m.Unit})");
            var termSnippets = terms.Take(6).Select(t => $"Term: '{t.Name}' -> {t.Definition}");
            semanticContext = $"""

                Semantic Knowledge Context:
                {string.Join("\n", metricSnippets)}
                {string.Join("\n", termSnippets)}
                """;
        }

        // 2. Retrieve Schema Metadata if data source is specified
        string schemaContext = "";
        if (request.DataSourceId.HasValue && _schemaReadStore is not null)
        {
            var tables = await _schemaReadStore.GetTablesAsync(request.WorkspaceId, request.DataSourceId, cancellationToken);
            if (tables.Count > 0)
            {
                var tableNames = tables.Take(8).Select(t => t.TableName);
                schemaContext = $"""

                    Available Database Tables:
                    {string.Join(", ", tableNames)}
                    """;
            }
        }

        string fullPrompt = $"""
            User Dashboard Request: "{request.Prompt}"
            Organization / Workspace: "{request.WorkspaceName ?? "Business Organization"}"
            Target Time Horizon: "{request.DateRange ?? "Last 30 Days"}"
            {semanticContext}
            {schemaContext}

            Formulate a complete, safe, structured dashboard definition with 4 to 8 widgets adhering to the DashboardAgentResult JSON schema.
            """;

        var agentRequest = new AgentRequest(fullPrompt, request.WorkspaceId.ToString());
        var response = await _agent.ExecuteAsync(agentRequest, cancellationToken);

        stopwatch.Stop();
        _telemetry.TrackAgentExecution("DashboardCoordinator.GenerateDashboard", stopwatch.ElapsedMilliseconds, response.TotalTokens, response.IsSuccess);

        if (!response.IsSuccess)
        {
            _logger.LogError("DashboardGeneratorAgent execution failed: {Error}", response.Content);
            return Result.Failure<DashboardAgentResult>(Error.Failure("Dashboard.AgentFailed", response.Content));
        }

        var result = _agent.ParseDashboardResult(response.Content);
        return Result.Success(result);
    }

    public async Task<Result<ExecuteWidgetQueryResponse>> ExecuteWidgetQueryAsync(
        ExecuteWidgetQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.SqlQuery))
        {
            return Result.Success(new ExecuteWidgetQueryResponse(Array.Empty<string>(), Array.Empty<IReadOnlyDictionary<string, object?>>(), 0, 0));
        }

        if (!request.DataSourceId.HasValue || _dataSourceStore is null || _encryptionService is null || _sqlExecutionTools is null)
        {
            return Result.Success(new ExecuteWidgetQueryResponse(
                new List<string> { "label", "value" },
                new List<IReadOnlyDictionary<string, object?>>
                {
                    new Dictionary<string, object?> { ["label"] = "Nominal", ["value"] = 100 }
                },
                5,
                1));
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var ds = await _dataSourceStore.GetByIdAsync(request.DataSourceId.Value, cancellationToken);
            if (ds is null)
            {
                return Result.Failure<ExecuteWidgetQueryResponse>(Error.NotFound("DataSource.NotFound", "Data source not found."));
            }

            string connStr = _encryptionService.Decrypt(ds.EncryptedConnectionString);
            var queryResult = await _sqlExecutionTools.ExecuteSafeQueryAsync(
                ds.DatabaseType,
                connStr,
                request.SqlQuery,
                timeoutSeconds: 15,
                cancellationToken);

            stopwatch.Stop();

            if (!queryResult.IsSuccess)
            {
                return Result.Failure<ExecuteWidgetQueryResponse>(Error.Failure("Widget.QueryExecutionFailed", queryResult.ErrorMessage ?? "Query execution failed."));
            }

            var widgetRows = queryResult.Rows;
            if (_maskingEngine != null && _governanceStore != null)
            {
                var policies = await _governanceStore.GetByWorkspaceIdAsync(request.WorkspaceId, activeOnly: true, cancellationToken);
                if (policies.Count > 0)
                {
                    widgetRows = _maskingEngine.MaskRows(widgetRows, tableName: null, policies, userRole: "Member", isAiContext: false);
                }
            }

            return Result.Success(new ExecuteWidgetQueryResponse(
                queryResult.Columns,
                widgetRows,
                queryResult.ExecutionDurationMs,
                queryResult.RowCount));
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogWarning(ex, "Widget query execution failed: {Sql}", request.SqlQuery);
            return Result.Failure<ExecuteWidgetQueryResponse>(Error.Failure("Widget.QueryExecutionFailed", ex.Message));
        }
    }
}
