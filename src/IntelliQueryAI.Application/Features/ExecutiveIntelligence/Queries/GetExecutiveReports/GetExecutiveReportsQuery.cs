using System.Text.Json;
using IntelliQueryAI.AI.Agents;
using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Application.Features.ExecutiveIntelligence.Dtos;
using IntelliQueryAI.Domain.Entities.Executive;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.SharedKernel.Results;

namespace IntelliQueryAI.Application.Features.ExecutiveIntelligence.Queries.GetExecutiveReports;

public sealed record GetExecutiveReportsQuery(Guid WorkspaceId, int Limit = 20) : IQuery<Result<IReadOnlyList<ExecutiveReportDto>>>;

public sealed class GetExecutiveReportsQueryHandler : IQueryHandler<GetExecutiveReportsQuery, Result<IReadOnlyList<ExecutiveReportDto>>>
{
    private readonly IExecutiveReportStore _store;
    private readonly IWorkspaceStore? _workspaceStore;

    public GetExecutiveReportsQueryHandler(
        IExecutiveReportStore store,
        IWorkspaceStore? workspaceStore = null)
    {
        _store = store;
        _workspaceStore = workspaceStore;
    }

    public async Task<Result<IReadOnlyList<ExecutiveReportDto>>> HandleAsync(
        GetExecutiveReportsQuery query,
        CancellationToken cancellationToken = default)
    {
        string? workspaceName = null;
        if (_workspaceStore is not null)
        {
            var ws = await _workspaceStore.GetByIdAsync(query.WorkspaceId, cancellationToken);
            workspaceName = ws?.Name;
        }

        var reports = await _store.GetByWorkspaceIdAsync(query.WorkspaceId, query.Limit, cancellationToken);

        var dtos = reports.Select(r => MapToDto(r, workspaceName)).ToList();
        return Result.Success<IReadOnlyList<ExecutiveReportDto>>(dtos);
    }

    internal static ExecutiveReportDto MapToDto(ExecutiveReport r, string? workspaceName = null)
    {
        IReadOnlyList<string> findings = DeserializeList(r.Findings);
        IReadOnlyList<string> risks = DeserializeList(r.Risks);
        IReadOnlyList<string> opportunities = DeserializeList(r.Opportunities);
        IReadOnlyList<ActionableRecommendation> recommendations = DeserializeRecommendations(r.Recommendations);

        string summary = r.Summary;
        string title = r.Title;

        if (!string.IsNullOrWhiteSpace(workspaceName))
        {
            summary = SanitizeText(summary, r.WorkspaceId, workspaceName);
            title = SanitizeText(title, r.WorkspaceId, workspaceName);
            findings = findings.Select(f => SanitizeText(f, r.WorkspaceId, workspaceName)).ToList();
            risks = risks.Select(rk => SanitizeText(rk, r.WorkspaceId, workspaceName)).ToList();
            opportunities = opportunities.Select(o => SanitizeText(o, r.WorkspaceId, workspaceName)).ToList();
            recommendations = recommendations.Select(rc => rc with
            {
                Rationale = SanitizeText(rc.Rationale, r.WorkspaceId, workspaceName),
                ActionSteps = (rc.ActionSteps ?? Array.Empty<string>()).Select(step => SanitizeText(step, r.WorkspaceId, workspaceName)).ToList()
            }).ToList();
        }

        return new ExecutiveReportDto(
            r.Id,
            r.WorkspaceId,
            title,
            r.ReportPeriod,
            summary,
            findings,
            risks,
            opportunities,
            recommendations,
            r.GeneratedAtUtc);
    }

    public static string SanitizeText(string text, Guid workspaceId, string workspaceName)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;
        string guidStr = workspaceId.ToString();
        bool nameContainsWorkspace = workspaceName.Contains("Workspace", StringComparison.OrdinalIgnoreCase);
        string workspacePrefix = nameContainsWorkspace ? workspaceName : $"Workspace '{workspaceName}'";

        return text
            .Replace($"Workspace {guidStr}", workspacePrefix, StringComparison.OrdinalIgnoreCase)
            .Replace($"workspace {guidStr}", workspacePrefix, StringComparison.OrdinalIgnoreCase)
            .Replace(guidStr, workspaceName, StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> DeserializeList(string json)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(json))
            {
                var list = JsonSerializer.Deserialize<List<string>>(json);
                if (list != null) return list;
            }
        }
        catch { }
        return Array.Empty<string>();
    }

    private static IReadOnlyList<ActionableRecommendation> DeserializeRecommendations(string json)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(json))
            {
                var list = JsonSerializer.Deserialize<List<ActionableRecommendation>>(json);
                if (list != null) return list;
            }
        }
        catch { }
        return Array.Empty<ActionableRecommendation>();
    }
}
