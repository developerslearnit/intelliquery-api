using IntelliQueryAI.Domain.Enums;

namespace IntelliQueryAI.Application.Features.Ai.Dtos;

public sealed record ChartDto(
    string RecommendedChart,
    string Title,
    string XAxisColumn,
    IReadOnlyList<string> YAxisColumns,
    string Description,
    IReadOnlyDictionary<string, string>? StylingOptions);

public sealed record ChatQueryResponseDto(
    string Question,
    string GeneratedSql,
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows,
    int RowCount,
    long ExecutionDurationMs,
    IReadOnlyList<string> Insights,
    ChartDto RecommendedChart,
    double ConfidenceScore,
    long TotalDurationMs,
    int TotalTokensUsed);

public sealed record AnalysisResponseDto(
    string Summary,
    IReadOnlyList<string> KeyInsights,
    IReadOnlyList<string> Outliers,
    IReadOnlyList<string> Recommendations,
    ChartDto? RecommendedChart);

public sealed record SavedReportDto(
    Guid Id,
    Guid WorkspaceId,
    Guid UserId,
    string Title,
    string? Description,
    string Question,
    string SqlQuery,
    string ChartConfigurationJson,
    string InsightsJson,
    DateTime CreatedAtUtc,
    string? GeneratedSql = null,
    string? ChartType = null,
    DateTime? CreatedAt = null);

public sealed record QueryHistoryDto(
    Guid Id,
    Guid WorkspaceId,
    Guid DataSourceId,
    Guid UserId,
    string Question,
    string GeneratedSql,
    long ExecutionTimeMs,
    int RowCount,
    bool IsSuccessful,
    string? ErrorMessage,
    double ConfidenceScore,
    DateTime ExecutedAtUtc);
