using System.Text.Json;
using IntelliQueryAI.SharedKernel.Primitives;

namespace IntelliQueryAI.Domain.Entities.Recommendations;

public enum RecommendationPriority
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}

public enum RecommendationStatus
{
    Active = 0,
    UnderReview = 1,
    Applied = 2,
    Dismissed = 3
}

public sealed record RecommendationEvidenceItem(
    string Title,
    string Description,
    string? MetricName = null,
    decimal? MetricValue = null,
    decimal? BaselineValue = null,
    string? EvidenceType = null,
    string? LinkUrl = null);

public sealed record RecommendationActionStep(
    int StepNumber,
    string Action,
    string? ExpectedImpact = null,
    bool AutomatedActionPossible = false);

/// <summary>
/// Aggregate root representing an evidence-backed, actionable business recommendation.
/// Core Invariant: Never automatically perform destructive business actions without human-in-the-loop review.
/// </summary>
public sealed class Recommendation : AggregateRoot
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public Guid WorkspaceId { get; private set; }
    public Guid? CreatedByUserId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Rationale { get; private set; } = string.Empty;
    public string Category { get; private set; } = string.Empty;
    public RecommendationPriority Priority { get; private set; }
    public RecommendationStatus Status { get; private set; }
    public string? TargetMetric { get; private set; }
    public string? ImpactMagnitude { get; private set; }
    public string? EstimatedEffort { get; private set; }
    public string EvidenceJson { get; private set; } = "[]";
    public string ActionStepsJson { get; private set; } = "[]";
    public Guid? RelatedInvestigationId { get; private set; }
    public Guid? RelatedAlertId { get; private set; }
    public Guid? RelatedMonitorId { get; private set; }
    public Guid? RelatedScenarioId { get; private set; }
    public DateTime? AppliedAtUtc { get; private set; }
    public DateTime? DismissedAtUtc { get; private set; }
    public string? DismissalReason { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    private Recommendation() { }

    public Recommendation(
        Guid workspaceId,
        Guid? createdByUserId,
        string title,
        string rationale,
        string category,
        RecommendationPriority priority,
        string? targetMetric = null,
        string? impactMagnitude = null,
        string? estimatedEffort = null,
        IReadOnlyList<RecommendationEvidenceItem>? evidence = null,
        IReadOnlyList<RecommendationActionStep>? actionSteps = null,
        Guid? relatedInvestigationId = null,
        Guid? relatedAlertId = null,
        Guid? relatedMonitorId = null,
        Guid? relatedScenarioId = null)
    {
        Id = Guid.NewGuid();
        WorkspaceId = workspaceId;
        CreatedByUserId = createdByUserId;
        Title = title;
        Rationale = rationale;
        Category = string.IsNullOrWhiteSpace(category) ? "Strategic" : category;
        Priority = priority;
        Status = RecommendationStatus.Active;
        TargetMetric = targetMetric;
        ImpactMagnitude = impactMagnitude;
        EstimatedEffort = estimatedEffort;
        EvidenceJson = JsonSerializer.Serialize(evidence ?? Array.Empty<RecommendationEvidenceItem>(), JsonOptions);
        ActionStepsJson = JsonSerializer.Serialize(actionSteps ?? Array.Empty<RecommendationActionStep>(), JsonOptions);
        RelatedInvestigationId = relatedInvestigationId;
        RelatedAlertId = relatedAlertId;
        RelatedMonitorId = relatedMonitorId;
        RelatedScenarioId = relatedScenarioId;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public IReadOnlyList<RecommendationEvidenceItem> GetEvidence()
    {
        if (string.IsNullOrWhiteSpace(EvidenceJson))
            return Array.Empty<RecommendationEvidenceItem>();

        try
        {
            return JsonSerializer.Deserialize<IReadOnlyList<RecommendationEvidenceItem>>(EvidenceJson, JsonOptions)
                   ?? Array.Empty<RecommendationEvidenceItem>();
        }
        catch
        {
            return Array.Empty<RecommendationEvidenceItem>();
        }
    }

    public IReadOnlyList<RecommendationActionStep> GetActionSteps()
    {
        if (string.IsNullOrWhiteSpace(ActionStepsJson))
            return Array.Empty<RecommendationActionStep>();

        try
        {
            return JsonSerializer.Deserialize<IReadOnlyList<RecommendationActionStep>>(ActionStepsJson, JsonOptions)
                   ?? Array.Empty<RecommendationActionStep>();
        }
        catch
        {
            return Array.Empty<RecommendationActionStep>();
        }
    }

    public void MarkApplied(Guid userId)
    {
        Status = RecommendationStatus.Applied;
        AppliedAtUtc = DateTime.UtcNow;
        DismissedAtUtc = null;
        DismissalReason = null;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Dismiss(Guid userId, string? reason)
    {
        Status = RecommendationStatus.Dismissed;
        DismissedAtUtc = DateTime.UtcNow;
        DismissalReason = reason;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void SetUnderReview(Guid userId)
    {
        Status = RecommendationStatus.UnderReview;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Reactivate(Guid userId)
    {
        Status = RecommendationStatus.Active;
        AppliedAtUtc = null;
        DismissedAtUtc = null;
        DismissalReason = null;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void UpdateContent(
        string title,
        string rationale,
        string category,
        RecommendationPriority priority,
        string? targetMetric,
        string? impactMagnitude,
        string? estimatedEffort,
        IReadOnlyList<RecommendationEvidenceItem>? evidence,
        IReadOnlyList<RecommendationActionStep>? actionSteps)
    {
        Title = title;
        Rationale = rationale;
        Category = string.IsNullOrWhiteSpace(category) ? "Strategic" : category;
        Priority = priority;
        TargetMetric = targetMetric;
        ImpactMagnitude = impactMagnitude;
        EstimatedEffort = estimatedEffort;

        if (evidence != null)
        {
            EvidenceJson = JsonSerializer.Serialize(evidence, JsonOptions);
        }

        if (actionSteps != null)
        {
            ActionStepsJson = JsonSerializer.Serialize(actionSteps, JsonOptions);
        }

        UpdatedAtUtc = DateTime.UtcNow;
    }
}
