using System.Text.Json;
using IntelliQueryAI.AI.Agents;
using IntelliQueryAI.Domain.Entities.Investigations;

namespace IntelliQueryAI.Application.Features.Investigations.Dtos;

public sealed record InvestigationContributorDto(
    string Dimension,
    string Segment,
    double ContributionPercentage,
    string ImpactDirection,
    decimal BaselineValue,
    decimal CurrentValue,
    decimal ChangeValue,
    string Explanation);

public sealed record InvestigationEvidenceDto(
    string Title,
    string Description,
    string EvidenceType,
    string? MetricName,
    string? DataPointsJson,
    string? ChartType);

public sealed record InvestigationQueryExecutedDto(
    string Purpose,
    string SqlQuery,
    int RowCount,
    long DurationMs);

public sealed record InvestigationReportDto(
    Guid Id,
    Guid WorkspaceId,
    Guid? DataSourceId,
    string Question,
    string? TargetMetric,
    string Summary,
    double ConfidenceScore,
    string Status,
    IReadOnlyList<string> RootCauses,
    IReadOnlyList<InvestigationContributorDto> Contributors,
    IReadOnlyList<InvestigationEvidenceDto> Evidence,
    IReadOnlyList<ActionableRecommendation> Recommendations,
    IReadOnlyList<InvestigationQueryExecutedDto> QueriesExecuted,
    IReadOnlyList<string> FollowUpQuestions,
    long DurationMs,
    int TokensUsed,
    DateTime CreatedAtUtc)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static InvestigationReportDto FromDomain(InvestigationReport report)
    {
        var rootCauses = DeserializeList<string>(report.RootCausesJson);
        var contributors = DeserializeList<InvestigationContributorDto>(report.ContributorsJson);
        var evidence = DeserializeList<InvestigationEvidenceDto>(report.EvidenceJson);
        var recommendations = DeserializeList<ActionableRecommendation>(report.RecommendationsJson);
        var queries = DeserializeList<InvestigationQueryExecutedDto>(report.QueriesExecutedJson);
        var followUps = DeserializeList<string>(report.FollowUpQuestionsJson);

        return new InvestigationReportDto(
            report.Id,
            report.WorkspaceId,
            report.DataSourceId,
            report.Question,
            report.TargetMetric,
            report.Summary,
            report.ConfidenceScore,
            report.Status,
            rootCauses,
            contributors,
            evidence,
            recommendations,
            queries,
            followUps,
            report.DurationMs,
            report.TokensUsed,
            report.CreatedAtUtc);
    }

    private static IReadOnlyList<T> DeserializeList<T>(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<T>();
        try
        {
            return JsonSerializer.Deserialize<List<T>>(json, JsonOptions) ?? (IReadOnlyList<T>)Array.Empty<T>();
        }
        catch
        {
            return Array.Empty<T>();
        }
    }
}
