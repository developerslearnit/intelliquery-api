using System.Text;
using IntelliQueryAI.AI.Agents;
using IntelliQueryAI.Domain.Entities.Recommendations;
using IntelliQueryAI.Domain.Persistence;
using Microsoft.Extensions.Logging;

namespace IntelliQueryAI.AI.Coordinator;

public interface IRecommendationCoordinator
{
    Task<IReadOnlyList<Recommendation>> GenerateRecommendationsAsync(
        Guid workspaceId,
        Guid? userId,
        CancellationToken cancellationToken = default);
}

public sealed class RecommendationCoordinator : IRecommendationCoordinator
{
    private readonly RecommendationAnalystAgent _analystAgent;
    private readonly IRecommendationStore _recommendationStore;
    private readonly IInvestigationReportStore _investigationStore;
    private readonly IScenarioStore _scenarioStore;
    private readonly IAlertStore _alertStore;
    private readonly IMetricStore _metricStore;
    private readonly ILogger<RecommendationCoordinator> _logger;

    public RecommendationCoordinator(
        RecommendationAnalystAgent analystAgent,
        IRecommendationStore recommendationStore,
        IInvestigationReportStore investigationStore,
        IScenarioStore scenarioStore,
        IAlertStore alertStore,
        IMetricStore metricStore,
        ILogger<RecommendationCoordinator> logger)
    {
        _analystAgent = analystAgent;
        _recommendationStore = recommendationStore;
        _investigationStore = investigationStore;
        _scenarioStore = scenarioStore;
        _alertStore = alertStore;
        _metricStore = metricStore;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Recommendation>> GenerateRecommendationsAsync(
        Guid workspaceId,
        Guid? userId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating evidence-backed recommendations for workspace {WorkspaceId}", workspaceId);

        // 1. Gather cross-cutting intelligence signals from workspace stores
        var investigations = await _investigationStore.GetByWorkspaceIdAsync(workspaceId, 5, cancellationToken);
        var scenarios = await _scenarioStore.GetByWorkspaceIdAsync(workspaceId, 5, cancellationToken);
        var alerts = await _alertStore.GetByWorkspaceIdAsync(workspaceId, onlyOpen: true, severity: null, limit: 5, cancellationToken: cancellationToken);
        var metrics = await _metricStore.GetByWorkspaceIdAsync(workspaceId, search: null, isActive: true, cancellationToken: cancellationToken);

        // 2. Build synthesis prompt
        var sb = new StringBuilder();
        sb.AppendLine($"Workspace Context for Workspace: {workspaceId}");

        if (metrics.Count > 0)
        {
            sb.AppendLine("\nCore Semantic Business Metrics:");
            foreach (var m in metrics.Take(8))
            {
                sb.AppendLine($"- {m.Name} ({m.Unit}): {m.Description} [Formula: {m.CalculationFormula}]");
            }
        }

        if (investigations.Count > 0)
        {
            sb.AppendLine("\nRecent Root-Cause Investigations:");
            foreach (var inv in investigations)
            {
                sb.AppendLine($"- ID: {inv.Id} | Question: '{inv.Question}' | Summary: {inv.Summary}");
            }
        }

        if (alerts.Count > 0)
        {
            sb.AppendLine("\nRecent Monitoring Alerts:");
            foreach (var a in alerts)
            {
                sb.AppendLine($"- ID: {a.Id} | Severity: {a.Severity} | Title: '{a.Title}' | Description: {a.Description}");
            }
        }

        if (scenarios.Count > 0)
        {
            sb.AppendLine("\nRecent What-If Scenario Simulations:");
            foreach (var sc in scenarios)
            {
                var exp = sc.GetProjections().FirstOrDefault(p => p.CaseType.Equals("Expected", StringComparison.OrdinalIgnoreCase));
                sb.AppendLine($"- ID: {sc.Id} | '{sc.Title}' | Metric: {sc.TargetMetric} | Horizon: {sc.TimeHorizon} | Expected Delta: {exp?.DeltaPercentage:+#0.0;-#0.0;0.0}%");
            }
        }

        if (investigations.Count == 0 && alerts.Count == 0 && scenarios.Count == 0)
        {
            sb.AppendLine("\nGeneral operational baseline telemetry. Synthesize foundational operational recommendations.");
        }

        // 3. Invoke Microsoft Agent Framework RecommendationAnalystAgent
        var generatedItems = await _analystAgent.SynthesizeRecommendationsAsync(sb.ToString(), cancellationToken);

        // 4. Fetch existing active recommendations to prevent duplicate persistence
        var existingActive = await _recommendationStore.GetByWorkspaceAsync(
            workspaceId,
            status: RecommendationStatus.Active,
            limit: 50,
            ct: cancellationToken);

        var existingTitles = new HashSet<string>(existingActive.Select(r => r.Title.Trim().ToLowerInvariant()));
        var persisted = new List<Recommendation>();

        foreach (var item in generatedItems)
        {
            if (string.IsNullOrWhiteSpace(item.Title)) continue;

            if (existingTitles.Contains(item.Title.Trim().ToLowerInvariant()))
            {
                _logger.LogDebug("Recommendation with title '{Title}' already exists in workspace {WorkspaceId}, skipping.", item.Title, workspaceId);
                continue;
            }

            if (!Enum.TryParse<RecommendationPriority>(item.Priority, true, out var priority))
            {
                priority = RecommendationPriority.Medium;
            }

            var entity = new Recommendation(
                workspaceId: workspaceId,
                createdByUserId: userId,
                title: item.Title.Trim(),
                rationale: item.Rationale?.Trim() ?? "Evidence-backed recommendation derived from platform telemetry.",
                category: item.Category?.Trim() ?? "Strategic",
                priority: priority,
                targetMetric: item.TargetMetric?.Trim(),
                impactMagnitude: item.ImpactMagnitude?.Trim(),
                estimatedEffort: item.EstimatedEffort?.Trim(),
                evidence: item.Evidence,
                actionSteps: item.ActionSteps,
                relatedInvestigationId: item.RelatedInvestigationId,
                relatedAlertId: item.RelatedAlertId,
                relatedMonitorId: item.RelatedMonitorId,
                relatedScenarioId: item.RelatedScenarioId);

            await _recommendationStore.AddAsync(entity, cancellationToken);
            persisted.Add(entity);
            existingTitles.Add(item.Title.Trim().ToLowerInvariant());
        }

        _logger.LogInformation("Successfully persisted {Count} new recommendations for workspace {WorkspaceId}", persisted.Count, workspaceId);

        // Return all currently active recommendations
        return await _recommendationStore.GetByWorkspaceAsync(
            workspaceId,
            status: null,
            limit: 50,
            ct: cancellationToken);
    }
}
