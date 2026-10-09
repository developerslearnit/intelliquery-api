using IntelliQueryAI.AI.Agents;

namespace IntelliQueryAI.Application.Features.ExecutiveIntelligence.Dtos;

public sealed record ExecutiveReportDto(
    Guid Id,
    Guid WorkspaceId,
    string Title,
    string ReportPeriod,
    string Summary,
    IReadOnlyList<string> Findings,
    IReadOnlyList<string> Risks,
    IReadOnlyList<string> Opportunities,
    IReadOnlyList<ActionableRecommendation> Recommendations,
    DateTime GeneratedAtUtc);

public sealed record GenerateExecutiveReportRequestDto(
    string? ReportPeriod = null,
    string? CustomTitle = null);
