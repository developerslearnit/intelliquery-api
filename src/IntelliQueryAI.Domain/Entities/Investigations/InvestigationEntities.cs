using System.Text.Json;
using IntelliQueryAI.Domain.Entities.Executive;
using IntelliQueryAI.SharedKernel.Primitives;

namespace IntelliQueryAI.Domain.Entities.Investigations;

public sealed record InvestigationContributor(
    string Dimension,
    string Segment,
    double ContributionPercentage,
    string ImpactDirection,
    decimal BaselineValue,
    decimal CurrentValue,
    decimal ChangeValue,
    string Explanation);

public sealed record InvestigationEvidence(
    string Title,
    string Description,
    string EvidenceType,
    string? MetricName,
    string? DataPointsJson,
    string? ChartType);

public sealed record InvestigationQueryExecuted(
    string Purpose,
    string SqlQuery,
    int RowCount,
    long DurationMs);

/// <summary>
/// Autonomous AI-generated multi-step root-cause investigation report.
/// Analyzes metric baselines, dimension breakdowns, anomaly correlations, and produces verified evidence.
/// </summary>
public sealed class InvestigationReport : AggregateRoot
{
    public Guid WorkspaceId { get; private set; }
    public Guid? DataSourceId { get; private set; }
    public string Question { get; private set; } = string.Empty;
    public string? TargetMetric { get; private set; }
    public string Summary { get; private set; } = string.Empty;
    public double ConfidenceScore { get; private set; }
    public string Status { get; private set; } = "Completed";
    public string RootCausesJson { get; private set; } = "[]";
    public string ContributorsJson { get; private set; } = "[]";
    public string EvidenceJson { get; private set; } = "[]";
    public string RecommendationsJson { get; private set; } = "[]";
    public string QueriesExecutedJson { get; private set; } = "[]";
    public string FollowUpQuestionsJson { get; private set; } = "[]";
    public long DurationMs { get; private set; }
    public int TokensUsed { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private InvestigationReport(
        Guid workspaceId,
        Guid? dataSourceId,
        string question,
        string? targetMetric,
        string summary,
        double confidenceScore,
        string status,
        string rootCausesJson,
        string contributorsJson,
        string evidenceJson,
        string recommendationsJson,
        string queriesExecutedJson,
        string followUpQuestionsJson,
        long durationMs,
        int tokensUsed)
        : base(Guid.NewGuid())
    {
        WorkspaceId = workspaceId;
        DataSourceId = dataSourceId;
        Question = question;
        TargetMetric = targetMetric;
        Summary = summary;
        ConfidenceScore = confidenceScore;
        Status = status;
        RootCausesJson = rootCausesJson;
        ContributorsJson = contributorsJson;
        EvidenceJson = evidenceJson;
        RecommendationsJson = recommendationsJson;
        QueriesExecutedJson = queriesExecutedJson;
        FollowUpQuestionsJson = followUpQuestionsJson;
        DurationMs = durationMs;
        TokensUsed = tokensUsed;
        CreatedAtUtc = DateTime.UtcNow;
    }

    private InvestigationReport() { }

    public static InvestigationReport Create(
        Guid workspaceId,
        Guid? dataSourceId,
        string question,
        string? targetMetric,
        string summary,
        double confidenceScore,
        string status,
        string rootCausesJson,
        string contributorsJson,
        string evidenceJson,
        string recommendationsJson,
        string queriesExecutedJson,
        string followUpQuestionsJson,
        long durationMs,
        int tokensUsed)
    {
        return new InvestigationReport(
            workspaceId,
            dataSourceId,
            question,
            targetMetric,
            summary,
            confidenceScore,
            status,
            rootCausesJson,
            contributorsJson,
            evidenceJson,
            recommendationsJson,
            queriesExecutedJson,
            followUpQuestionsJson,
            durationMs,
            tokensUsed);
    }
}
