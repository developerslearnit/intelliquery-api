namespace IntelliQueryAI.Application.Features.Recommendations.Dtos;

public sealed record RecommendationEvidenceItemDto(
    string Title,
    string Description,
    string? MetricName = null,
    decimal? MetricValue = null,
    decimal? BaselineValue = null,
    string? EvidenceType = null,
    string? LinkUrl = null);

public sealed record RecommendationActionStepDto(
    int StepNumber,
    string Action,
    string? ExpectedImpact = null,
    bool AutomatedActionPossible = false);

public sealed record RecommendationDto(
    Guid Id,
    Guid WorkspaceId,
    Guid? CreatedByUserId,
    string Title,
    string Rationale,
    string Category,
    string Priority,
    string Status,
    string? TargetMetric,
    string? ImpactMagnitude,
    string? EstimatedEffort,
    IReadOnlyList<RecommendationEvidenceItemDto> Evidence,
    IReadOnlyList<RecommendationActionStepDto> ActionSteps,
    Guid? RelatedInvestigationId,
    Guid? RelatedAlertId,
    Guid? RelatedMonitorId,
    Guid? RelatedScenarioId,
    DateTime? AppliedAtUtc,
    DateTime? DismissedAtUtc,
    string? DismissalReason,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed record CreateRecommendationRequestDto(
    string Title,
    string Rationale,
    string Category,
    string Priority,
    string? TargetMetric = null,
    string? ImpactMagnitude = null,
    string? EstimatedEffort = null,
    IReadOnlyList<RecommendationEvidenceItemDto>? Evidence = null,
    IReadOnlyList<RecommendationActionStepDto>? ActionSteps = null,
    Guid? RelatedInvestigationId = null,
    Guid? RelatedAlertId = null,
    Guid? RelatedMonitorId = null,
    Guid? RelatedScenarioId = null);

public sealed record DismissRecommendationRequestDto(
    string? Reason = null);

public sealed record RecommendationSummaryDto(
    int TotalActive,
    int TotalCriticalActive,
    int TotalApplied,
    int TotalDismissed);
