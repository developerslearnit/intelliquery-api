using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using IntelliQueryAI.AI.Agents;
using IntelliQueryAI.AI.Telemetry;
using IntelliQueryAI.Domain.Entities.BusinessKnowledge;
using IntelliQueryAI.Domain.Entities.Executive;
using IntelliQueryAI.Domain.Entities.Investigations;
using IntelliQueryAI.Domain.Enums;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.Domain.Services;
using IntelliQueryAI.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace IntelliQueryAI.AI.Coordinator;

public sealed record InvestigationCoordinatorRequest(
    Guid WorkspaceId,
    Guid? DataSourceId,
    string Question,
    string? TargetMetric = null,
    string? WorkspaceName = null);

public sealed record InvestigationCoordinatorResponse(
    string Summary,
    double ConfidenceScore,
    string Status,
    IReadOnlyList<string> RootCauses,
    IReadOnlyList<InvestigationContributor> Contributors,
    IReadOnlyList<InvestigationEvidence> Evidence,
    IReadOnlyList<ActionableRecommendation> Recommendations,
    IReadOnlyList<InvestigationQueryExecuted> QueriesExecuted,
    IReadOnlyList<string> FollowUpQuestions,
    long DurationMs,
    int TokensUsed);

public interface IInvestigationCoordinator
{
    Task<Result<InvestigationCoordinatorResponse>> InvestigateAsync(
        InvestigationCoordinatorRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class InvestigationCoordinator : IInvestigationCoordinator
{
    private readonly RootCauseInvestigationAgent _investigationAgent;
    private readonly IBusinessTermStore? _businessTermStore;
    private readonly IMetricStore? _metricStore;
    private readonly ISchemaReadStore? _schemaReadStore;
    private readonly IDataSourceStore? _dataSourceStore;
    private readonly ISafeQueryExecutor? _safeQueryExecutor;
    private readonly IEncryptionService? _encryptionService;
    private readonly ITelemetryService _telemetry;
    private readonly ILogger<InvestigationCoordinator> _logger;

    // Safety guardrail limits
    private const int MaxInvestigationSteps = 5;
    private const int MaxDiagnosticQueries = 4;
    private const int QueryTimeoutSeconds = 10;

    public InvestigationCoordinator(
        RootCauseInvestigationAgent investigationAgent,
        ITelemetryService telemetry,
        ILogger<InvestigationCoordinator> logger,
        IBusinessTermStore? businessTermStore = null,
        IMetricStore? metricStore = null,
        ISchemaReadStore? schemaReadStore = null,
        IDataSourceStore? dataSourceStore = null,
        ISafeQueryExecutor? safeQueryExecutor = null,
        IEncryptionService? encryptionService = null)
    {
        _investigationAgent = investigationAgent;
        _telemetry = telemetry;
        _logger = logger;
        _businessTermStore = businessTermStore;
        _metricStore = metricStore;
        _schemaReadStore = schemaReadStore;
        _dataSourceStore = dataSourceStore;
        _safeQueryExecutor = safeQueryExecutor;
        _encryptionService = encryptionService;
    }

    public async Task<Result<InvestigationCoordinatorResponse>> InvestigateAsync(
        InvestigationCoordinatorRequest request,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        int totalTokens = 0;
        var queriesExecuted = new List<InvestigationQueryExecuted>();

        _logger.LogInformation(
            "Starting Root-Cause Investigation for workspace '{WorkspaceId}' on question: {Question}",
            request.WorkspaceId, request.Question);

        try
        {
            // 1. Semantic Layer: Retrieve relevant business vocabulary and metric definitions (Phase A integration)
            var questionTokens = ExtractKeywords(request.Question);
            IReadOnlyList<BusinessTerm> relevantTerms = Array.Empty<BusinessTerm>();
            IReadOnlyList<MetricDefinition> relevantMetrics = Array.Empty<MetricDefinition>();

            if (_businessTermStore != null)
            {
                relevantTerms = await _businessTermStore.SearchRelevantTermsAsync(
                    request.WorkspaceId, questionTokens, cancellationToken);
            }

            if (_metricStore != null)
            {
                relevantMetrics = await _metricStore.SearchRelevantMetricsAsync(
                    request.WorkspaceId, questionTokens, cancellationToken);
            }

            // 2. Discover dimensions & schema objects
            var discoveredDimensions = new List<string>();
            var schemaContextSummary = new StringBuilder();

            if (request.DataSourceId.HasValue && _schemaReadStore != null)
            {
                var tables = await _schemaReadStore.GetTablesAsync(
                    request.WorkspaceId, request.DataSourceId.Value, cancellationToken);

                if (tables.Count > 0)
                {
                    var columns = await _schemaReadStore.GetColumnsAsync(
                        request.WorkspaceId, request.DataSourceId.Value, null, cancellationToken);

                    // Identify potential dimensions (categorical columns: status, type, region, tier, category, etc.)
                    var dimensionCandidates = columns
                        .Where(c => c.DataType.Contains("char", StringComparison.OrdinalIgnoreCase) ||
                                    c.DataType.Contains("varchar", StringComparison.OrdinalIgnoreCase) ||
                                    c.DataType.Contains("text", StringComparison.OrdinalIgnoreCase) ||
                                    c.ColumnName.Contains("status", StringComparison.OrdinalIgnoreCase) ||
                                    c.ColumnName.Contains("tier", StringComparison.OrdinalIgnoreCase) ||
                                    c.ColumnName.Contains("type", StringComparison.OrdinalIgnoreCase) ||
                                    c.ColumnName.Contains("region", StringComparison.OrdinalIgnoreCase) ||
                                    c.ColumnName.Contains("category", StringComparison.OrdinalIgnoreCase))
                        .Take(10)
                        .Select(c => $"{c.TableName}.{c.ColumnName}")
                        .ToList();

                    discoveredDimensions.AddRange(dimensionCandidates);

                    schemaContextSummary.AppendLine($"Cataloged Tables: {string.Join(", ", tables.Select(t => t.TableName).Take(8))}");
                    if (discoveredDimensions.Count > 0)
                    {
                        schemaContextSummary.AppendLine($"Candidate Dimensions: {string.Join(", ", discoveredDimensions.Take(6))}");
                    }
                }
            }

            // 3. Assemble Rich Evidence & Agent Investigation Prompt
            var promptBuilder = new StringBuilder();
            string orgDisplayName = !string.IsNullOrWhiteSpace(request.WorkspaceName)
                ? request.WorkspaceName
                : "the organization";

            promptBuilder.AppendLine($"Investigation Target Organization: {orgDisplayName}");
            promptBuilder.AppendLine($"Diagnostic Question: {request.Question}");
            if (!string.IsNullOrWhiteSpace(request.TargetMetric))
            {
                promptBuilder.AppendLine($"Target Metric Specified: {request.TargetMetric}");
            }

            if (relevantTerms.Count > 0 || relevantMetrics.Count > 0)
            {
                promptBuilder.AppendLine();
                promptBuilder.AppendLine("--- Business Knowledge & Semantic Layer ---");
                foreach (var term in relevantTerms.Take(5))
                {
                    promptBuilder.AppendLine($"- Term: '{term.Name}': {term.Definition}");
                    if (!string.IsNullOrWhiteSpace(term.TargetTable))
                    {
                        promptBuilder.AppendLine($"  (Mapping hint: {term.TargetTable}.{term.TargetColumn} | Rule: {term.SqlCalculationRule})");
                    }
                }

                foreach (var metric in relevantMetrics.Take(5))
                {
                    promptBuilder.AppendLine($"- Metric: '{metric.Name}' [{metric.Unit}]: {metric.Description}");
                    promptBuilder.AppendLine($"  Formula: {metric.CalculationFormula}");
                }
            }

            if (schemaContextSummary.Length > 0)
            {
                promptBuilder.AppendLine();
                promptBuilder.AppendLine("--- Discovered Data Architecture Context ---");
                promptBuilder.AppendLine(schemaContextSummary.ToString());
            }

            promptBuilder.AppendLine();
            promptBuilder.AppendLine("--- Investigation Instructions ---");
            promptBuilder.AppendLine("Execute a structured root-cause analysis based on baseline deviations, candidate dimensions, and anomalies.");
            promptBuilder.AppendLine("Provide structured JSON containing summary, confidence, rootCauses, contributors, evidence, recommendations, and followUpQuestions.");

            // 4. Delegate to RootCauseInvestigationAgent via Microsoft Agent Framework
            var agentRequest = new AgentRequest(promptBuilder.ToString());
            var agentResponse = await _investigationAgent.ExecuteAsync(agentRequest, cancellationToken);
            totalTokens += agentResponse.TotalTokens;

            var findings = _investigationAgent.ParseFindings(agentResponse.Content);

            // 5. Build Final Domain Records & Sanitize Leaked Technical GUIDs
            var sanitizedSummary = SanitizeText(findings.Summary, request.WorkspaceId, orgDisplayName);
            var sanitizedRootCauses = findings.RootCauses
                .Select(rc => SanitizeText(rc, request.WorkspaceId, orgDisplayName))
                .ToList();

            var contributors = findings.Contributors.Select(c => new InvestigationContributor(
                Dimension: c.Dimension,
                Segment: c.Segment,
                ContributionPercentage: c.ContributionPercentage,
                ImpactDirection: c.ImpactDirection,
                BaselineValue: c.BaselineValue,
                CurrentValue: c.CurrentValue,
                ChangeValue: c.ChangeValue,
                Explanation: SanitizeText(c.Explanation, request.WorkspaceId, orgDisplayName)
            )).ToList();

            var evidenceList = findings.Evidence.Select(e => new InvestigationEvidence(
                Title: e.Title,
                Description: SanitizeText(e.Description, request.WorkspaceId, orgDisplayName),
                EvidenceType: e.EvidenceType,
                MetricName: e.MetricName,
                DataPointsJson: e.DataPointsJson,
                ChartType: e.ChartType
            )).ToList();

            var recommendations = findings.Recommendations.Select(r => new ActionableRecommendation(
                Title: r.Title,
                Category: r.Category,
                Priority: r.Priority,
                Rationale: SanitizeText(r.Rationale, request.WorkspaceId, orgDisplayName),
                ActionSteps: r.ActionSteps?.Select(step => SanitizeText(step, request.WorkspaceId, orgDisplayName)).ToList()
                             ?? new List<string>()
            )).ToList();

            var followUps = findings.FollowUpQuestions
                .Select(q => SanitizeText(q, request.WorkspaceId, orgDisplayName))
                .ToList();

            stopwatch.Stop();
            long totalDuration = stopwatch.ElapsedMilliseconds;

            _telemetry.TrackAgentExecution("InvestigationCoordinator", totalDuration, totalTokens, true);

            var result = new InvestigationCoordinatorResponse(
                Summary: sanitizedSummary,
                ConfidenceScore: Math.Clamp(findings.Confidence, 0.5, 0.99),
                Status: "Completed",
                RootCauses: sanitizedRootCauses,
                Contributors: contributors,
                Evidence: evidenceList,
                Recommendations: recommendations,
                QueriesExecuted: queriesExecuted,
                FollowUpQuestions: followUps,
                DurationMs: totalDuration,
                TokensUsed: totalTokens
            );

            return Result.Success(result);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Root-cause investigation pipeline failed for workspace {WorkspaceId}", request.WorkspaceId);
            _telemetry.TrackAgentExecution("InvestigationCoordinator", stopwatch.ElapsedMilliseconds, totalTokens, false);
            return Result.Failure<InvestigationCoordinatorResponse>(
                Error.Failure("Investigation.Failed", $"Investigation pipeline encountered an unexpected error: {ex.Message}"));
        }
    }

    private static IReadOnlyList<string> ExtractKeywords(string question)
    {
        if (string.IsNullOrWhiteSpace(question)) return Array.Empty<string>();

        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "why", "did", "what", "caused", "the", "in", "last", "month", "quarter", "year",
            "show", "me", "how", "come", "where", "is", "are", "a", "an", "of", "and", "to", "for"
        };

        return question
            .Split(new[] { ' ', ',', '.', '?', '!', ';', ':', '-', '_' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 3 && !stopWords.Contains(w))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string SanitizeText(string text, Guid workspaceId, string friendlyName)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;

        string guidStr = workspaceId.ToString();
        string clean = text;

        clean = Regex.Replace(clean, $@"Workspace\s+{Regex.Escape(guidStr)}", friendlyName, RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, Regex.Escape(guidStr), friendlyName, RegexOptions.IgnoreCase);

        // Strip any residual raw GUID patterns in user-facing summaries
        clean = Regex.Replace(clean, @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b", friendlyName);

        return clean;
    }
}
