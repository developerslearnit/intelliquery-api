using IntelliQueryAI.SharedKernel.Primitives;

namespace IntelliQueryAI.Domain.Entities.Executive;

/// <summary>
/// Autonomous AI-generated executive briefing compiling findings, operational risks, strategic opportunities, and actionable recommendations.
/// </summary>
public sealed class ExecutiveReport : AggregateRoot
{
    public Guid WorkspaceId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string ReportPeriod { get; private set; } = string.Empty;
    public string Summary { get; private set; } = string.Empty;
    public string Findings { get; private set; } = string.Empty;
    public string Risks { get; private set; } = string.Empty;
    public string Opportunities { get; private set; } = string.Empty;
    public string Recommendations { get; private set; } = string.Empty;
    public DateTime GeneratedAtUtc { get; private set; }

    private ExecutiveReport(
        Guid workspaceId,
        string title,
        string reportPeriod,
        string summary,
        string findings,
        string risks,
        string opportunities,
        string recommendations)
        : base(Guid.NewGuid())
    {
        WorkspaceId = workspaceId;
        Title = title;
        ReportPeriod = reportPeriod;
        Summary = summary;
        Findings = findings;
        Risks = risks;
        Opportunities = opportunities;
        Recommendations = recommendations;
        GeneratedAtUtc = DateTime.UtcNow;
    }

    private ExecutiveReport() { }

    public static ExecutiveReport Create(
        Guid workspaceId,
        string title,
        string reportPeriod,
        string summary,
        string findings,
        string risks,
        string opportunities,
        string recommendations)
    {
        return new ExecutiveReport(
            workspaceId,
            title,
            reportPeriod,
            summary,
            findings,
            risks,
            opportunities,
            recommendations);
    }
}
