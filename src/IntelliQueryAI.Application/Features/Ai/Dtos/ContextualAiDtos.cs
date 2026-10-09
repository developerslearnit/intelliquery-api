using System.Text.Json.Serialization;
using IntelliQueryAI.Application.Common.Serialization;

namespace IntelliQueryAI.Application.Features.Ai.Dtos;

/// <summary>
/// Contextual AI payload originating from application components (KPIs, Alerts, Anomalies, Charts, Reports, Recommendations).
/// </summary>
public sealed record AiContextDto(
    [property: JsonConverter(typeof(FlexibleNullableGuidConverter))]
    Guid? WorkspaceId = null,
    string ContextType = "",
    [property: JsonConverter(typeof(FlexibleNullableGuidConverter))]
    Guid? EntityId = null,
    [property: JsonConverter(typeof(FlexibleNullableGuidConverter))]
    Guid? DataSourceId = null,
    string? Metric = null,
    string? DateRange = null,
    string? EntityTitle = null,
    [property: JsonConverter(typeof(FlexibleStringDictionaryConverter))]
    IReadOnlyDictionary<string, string>? AdditionalMetadata = null);

/// <summary>
/// Validated, server-enriched contextual intelligence resolution.
/// </summary>
public sealed record ResolvedAiContextDto(
    bool IsValid,
    string ContextType,
    Guid WorkspaceId,
    Guid? EntityId,
    Guid? DataSourceId,
    string? Metric,
    string? DateRange,
    string EntityTitle,
    string ContextSummary,
    string SuggestedPrompt,
    IReadOnlyList<string> SuggestedFollowUpQuestions,
    IReadOnlyDictionary<string, string> EnrichedMetadata,
    string? ErrorMessage = null);
