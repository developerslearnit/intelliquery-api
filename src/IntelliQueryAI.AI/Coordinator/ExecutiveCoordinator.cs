using System.Diagnostics;
using System.Text.Json;
using IntelliQueryAI.AI.Agents;
using IntelliQueryAI.AI.Telemetry;
using IntelliQueryAI.Domain.Entities.Executive;
using IntelliQueryAI.Domain.Enums;
using IntelliQueryAI.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace IntelliQueryAI.AI.Coordinator;

public sealed record EvaluateMonitorRequest(
    Guid WorkspaceId,
    Guid MonitorId,
    string MonitorName,
    MonitorType MonitorType,
    decimal CurrentMetricValue,
    decimal? BaselineMetricValue,
    IReadOnlyList<decimal>? HistoricalValues,
    string? ContextNotes = null);

public sealed record EvaluateMonitorResponse(
    bool TriggerAlert,
    AlertSeverity Severity,
    string Findings,
    string TrendSummary,
    IReadOnlyList<ActionableRecommendation> Recommendations,
    long DurationMs,
    int TokensUsed);

public sealed record GenerateExecutiveReportRequest(
    Guid WorkspaceId,
    string ReportPeriod,
    string? CustomTitle = null,
    IReadOnlyDictionary<string, decimal>? KeyMetrics = null,
    IReadOnlyList<string>? RecentAlertTitles = null,
    IReadOnlyList<string>? ActiveMonitorSummaries = null,
    string? WorkspaceName = null);

public sealed record GenerateExecutiveReportResponse(
    string Title,
    string ReportPeriod,
    string Summary,
    IReadOnlyList<string> KeyFindings,
    IReadOnlyList<string> Risks,
    IReadOnlyList<string> Opportunities,
    IReadOnlyList<ActionableRecommendation> Recommendations,
    long DurationMs,
    int TokensUsed);

public interface IExecutiveCoordinator
{
    Task<Result<EvaluateMonitorResponse>> EvaluateMonitorAsync(
        EvaluateMonitorRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<GenerateExecutiveReportResponse>> GenerateExecutiveBriefingAsync(
        GenerateExecutiveReportRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class ExecutiveCoordinator : IExecutiveCoordinator
{
    private readonly MonitoringAgent _monitoringAgent;
    private readonly AnomalyAgent _anomalyAgent;
    private readonly ExecutiveSummaryAgent _executiveSummaryAgent;
    private readonly RecommendationAgent _recommendationAgent;
    private readonly ITelemetryService _telemetry;
    private readonly ILogger<ExecutiveCoordinator> _logger;

    public ExecutiveCoordinator(
        MonitoringAgent monitoringAgent,
        AnomalyAgent anomalyAgent,
        ExecutiveSummaryAgent executiveSummaryAgent,
        RecommendationAgent recommendationAgent,
        ITelemetryService telemetry,
        ILogger<ExecutiveCoordinator> logger)
    {
        _monitoringAgent = monitoringAgent;
        _anomalyAgent = anomalyAgent;
        _executiveSummaryAgent = executiveSummaryAgent;
        _recommendationAgent = recommendationAgent;
        _telemetry = telemetry;
        _logger = logger;
    }

    public async Task<Result<EvaluateMonitorResponse>> EvaluateMonitorAsync(
        EvaluateMonitorRequest request,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        int totalTokens = 0;

        _logger.LogInformation("Starting AI Monitor evaluation for '{MonitorName}' ({MonitorId})", request.MonitorName, request.MonitorId);

        var prompt = $"""
            Evaluate the following operational AI Monitor:
            Monitor Name: {request.MonitorName}
            Monitor Type: {request.MonitorType}
            Current Metric Value: {request.CurrentMetricValue}
            Baseline Metric Value: {request.BaselineMetricValue?.ToString() ?? "N/A"}
            Historical Observations: {(request.HistoricalValues is not null ? string.Join(", ", request.HistoricalValues) : "None provided")}
            Context: {request.ContextNotes ?? "Standard scheduled evaluation"}
            """;

        var agentRequest = new AgentRequest(prompt, request.MonitorId.ToString());
        var monitoringResponse = await _monitoringAgent.ExecuteAsync(agentRequest, cancellationToken);
        totalTokens += monitoringResponse.TotalTokens;

        if (!monitoringResponse.IsSuccess)
        {
            _logger.LogError("MonitoringAgent execution failed: {Content}", monitoringResponse.Content);
            return Result.Failure<EvaluateMonitorResponse>(
                Error.Failure("Monitoring.AgentFailed", monitoringResponse.Content));
        }

        var findings = _monitoringAgent.ParseFindings(monitoringResponse.Content);

        // If alert is triggered or deviation is high, call AnomalyAgent to verify pattern
        var severity = Enum.TryParse<AlertSeverity>(findings.Severity, true, out var parsedSev)
            ? parsedSev
            : AlertSeverity.Information;

        var recommendations = new List<ActionableRecommendation>();

        if (findings.TriggerAlert || severity >= AlertSeverity.Warning)
        {
            var anomalyRequest = new AgentRequest($"""
                Classify anomaly severity and risk pattern for monitor '{request.MonitorName}':
                Findings: {findings.Findings}
                Trend: {findings.TrendSummary}
                Current Value: {request.CurrentMetricValue}
                Baseline: {request.BaselineMetricValue}
                """);

            var anomalyResponse = await _anomalyAgent.ExecuteAsync(anomalyRequest, cancellationToken);
            totalTokens += anomalyResponse.TotalTokens;

            if (anomalyResponse.IsSuccess)
            {
                var anomalyResult = _anomalyAgent.ParseClassification(anomalyResponse.Content);
                if (Enum.TryParse<AlertSeverity>(anomalyResult.Severity, true, out var anomalySev))
                {
                    if (anomalySev > severity)
                    {
                        severity = anomalySev;
                    }
                }
            }

            // Request actionable recommendations from RecommendationAgent
            var recRequest = new AgentRequest($"""
                Provide corrective recommendations for:
                Monitor: {request.MonitorName}
                Severity: {severity}
                Findings: {findings.Findings}
                Trend: {findings.TrendSummary}
                """);

            var recResponse = await _recommendationAgent.ExecuteAsync(recRequest, cancellationToken);
            totalTokens += recResponse.TotalTokens;

            if (recResponse.IsSuccess)
            {
                recommendations.AddRange(_recommendationAgent.ParseRecommendations(recResponse.Content));
            }
        }

        stopwatch.Stop();
        _telemetry.TrackAgentExecution("ExecutiveCoordinator.EvaluateMonitor", stopwatch.ElapsedMilliseconds, totalTokens, true);

        return Result.Success(new EvaluateMonitorResponse(
            findings.TriggerAlert,
            severity,
            findings.Findings,
            findings.TrendSummary,
            recommendations,
            stopwatch.ElapsedMilliseconds,
            totalTokens));
    }

    public async Task<Result<GenerateExecutiveReportResponse>> GenerateExecutiveBriefingAsync(
        GenerateExecutiveReportRequest request,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        int totalTokens = 0;

        _logger.LogInformation("Generating AI Executive Briefing for period '{Period}'", request.ReportPeriod);

        var metricsSummary = request.KeyMetrics is not null
            ? string.Join(", ", request.KeyMetrics.Select(kv => $"{kv.Key}: {kv.Value}"))
            : "Platform queries active, standard telemetry nominal.";

        var alertSummary = request.RecentAlertTitles is not null && request.RecentAlertTitles.Count > 0
            ? string.Join("; ", request.RecentAlertTitles)
            : "No critical alerts raised.";

        var monitorSummary = request.ActiveMonitorSummaries is not null && request.ActiveMonitorSummaries.Count > 0
            ? string.Join("; ", request.ActiveMonitorSummaries)
            : "All monitors operational.";

        string displayName = !string.IsNullOrWhiteSpace(request.WorkspaceName)
            ? request.WorkspaceName
            : "Workspace";

        var prompt = $"""
            Synthesize an executive business intelligence summary for workspace '{displayName}':
            Workspace Name: {displayName}
            Reporting Period: {request.ReportPeriod}
            Key Performance Metrics: {metricsSummary}
            Recent Alerts: {alertSummary}
            Active Monitors: {monitorSummary}

            Strict Persona & Formatting Rules:
            - Always refer to the workspace by its business name ('{displayName}') or as 'the organization/business'.
            - NEVER output internal database GUIDs, IDs, or technical hashes (like '{request.WorkspaceId}') in any human-facing text.
            """;

        var agentRequest = new AgentRequest(prompt);
        var execResponse = await _executiveSummaryAgent.ExecuteAsync(agentRequest, cancellationToken);
        totalTokens += execResponse.TotalTokens;

        if (!execResponse.IsSuccess)
        {
            return Result.Failure<GenerateExecutiveReportResponse>(
                Error.Failure("Executive.AgentFailed", execResponse.Content));
        }

        var briefing = _executiveSummaryAgent.ParseBriefing(execResponse.Content);

        // Defensive sanitization: replace any leaked workspace GUID with friendly workspace name
        string sanitizedSummary = SanitizeText(briefing.Summary, request.WorkspaceId, displayName);
        var sanitizedFindings = briefing.KeyFindings.Select(f => SanitizeText(f, request.WorkspaceId, displayName)).ToList();
        var sanitizedRisks = briefing.Risks.Select(r => SanitizeText(r, request.WorkspaceId, displayName)).ToList();
        var sanitizedOpportunities = briefing.Opportunities.Select(o => SanitizeText(o, request.WorkspaceId, displayName)).ToList();

        // Call RecommendationAgent to generate strategic and operational recommendations
        var recPrompt = $"""
            Based on this executive summary and identified risks/opportunities, formulate strategic recommendations for '{displayName}':
            Workspace Name: {displayName}
            Summary: {sanitizedSummary}
            Key Findings: {string.Join("; ", sanitizedFindings)}
            Risks: {string.Join("; ", sanitizedRisks)}
            Opportunities: {string.Join("; ", sanitizedOpportunities)}
            """;

        var recResponse = await _recommendationAgent.ExecuteAsync(new AgentRequest(recPrompt), cancellationToken);
        totalTokens += recResponse.TotalTokens;

        IReadOnlyList<ActionableRecommendation> rawRecommendations = recResponse.IsSuccess
            ? _recommendationAgent.ParseRecommendations(recResponse.Content)
            : Array.Empty<ActionableRecommendation>();

        var sanitizedRecommendations = rawRecommendations.Select(r => r with
        {
            Rationale = SanitizeText(r.Rationale, request.WorkspaceId, displayName),
            ActionSteps = (r.ActionSteps ?? Array.Empty<string>()).Select(step => SanitizeText(step, request.WorkspaceId, displayName)).ToList()
        }).ToList();

        stopwatch.Stop();
        _telemetry.TrackAgentExecution("ExecutiveCoordinator.GenerateExecutiveReport", stopwatch.ElapsedMilliseconds, totalTokens, true);

        string effectiveTitle = request.CustomTitle ?? SanitizeText(briefing.Title, request.WorkspaceId, displayName);

        return Result.Success(new GenerateExecutiveReportResponse(
            effectiveTitle,
            briefing.ReportPeriod,
            sanitizedSummary,
            sanitizedFindings,
            sanitizedRisks,
            sanitizedOpportunities,
            sanitizedRecommendations,
            stopwatch.ElapsedMilliseconds,
            totalTokens));
    }

    private static string SanitizeText(string text, Guid workspaceId, string workspaceName)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;
        string guidStr = workspaceId.ToString();
        bool nameContainsWorkspace = workspaceName.Contains("Workspace", StringComparison.OrdinalIgnoreCase);
        string workspacePrefix = nameContainsWorkspace ? workspaceName : $"Workspace '{workspaceName}'";

        return text
            .Replace($"Workspace {guidStr}", workspacePrefix, StringComparison.OrdinalIgnoreCase)
            .Replace($"workspace {guidStr}", workspacePrefix, StringComparison.OrdinalIgnoreCase)
            .Replace(guidStr, workspaceName, StringComparison.OrdinalIgnoreCase);
    }
}
