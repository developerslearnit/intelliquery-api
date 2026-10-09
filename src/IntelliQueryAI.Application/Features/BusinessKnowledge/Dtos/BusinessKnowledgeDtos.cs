using System.Text.Json;
using IntelliQueryAI.Domain.Entities.BusinessKnowledge;

namespace IntelliQueryAI.Application.Features.BusinessKnowledge.Dtos;

public sealed record BusinessTermDto(
    Guid Id,
    Guid WorkspaceId,
    string Name,
    string Definition,
    IReadOnlyList<string> Aliases,
    string Category,
    string Classification,
    string? OwnerName,
    string? OwnerEmail,
    string? TargetTable,
    string? TargetColumn,
    string? SqlCalculationRule,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc)
{
    public static BusinessTermDto FromDomain(BusinessTerm term)
    {
        IReadOnlyList<string> aliases = Array.Empty<string>();
        try
        {
            if (!string.IsNullOrWhiteSpace(term.AliasesJson))
            {
                aliases = JsonSerializer.Deserialize<List<string>>(term.AliasesJson) ?? (IReadOnlyList<string>)Array.Empty<string>();
            }
        }
        catch { }

        return new BusinessTermDto(
            term.Id,
            term.WorkspaceId,
            term.Name,
            term.Definition,
            aliases,
            term.Category,
            term.Classification,
            term.OwnerName,
            term.OwnerEmail,
            term.TargetTable,
            term.TargetColumn,
            term.SqlCalculationRule,
            term.IsActive,
            term.CreatedAtUtc,
            term.UpdatedAtUtc);
    }
}

public sealed record MetricDefinitionDto(
    Guid Id,
    Guid WorkspaceId,
    Guid? DataSourceId,
    string Name,
    string Description,
    string Unit,
    string CalculationFormula,
    string? FilterExpression,
    string? TargetTable,
    string? TargetColumn,
    string AggregationType,
    string TrendDirection,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc)
{
    public static MetricDefinitionDto FromDomain(MetricDefinition metric)
    {
        return new MetricDefinitionDto(
            metric.Id,
            metric.WorkspaceId,
            metric.DataSourceId,
            metric.Name,
            metric.Description,
            metric.Unit,
            metric.CalculationFormula,
            metric.FilterExpression,
            metric.TargetTable,
            metric.TargetColumn,
            metric.AggregationType,
            metric.TrendDirection,
            metric.IsActive,
            metric.CreatedAtUtc,
            metric.UpdatedAtUtc);
    }
}

public sealed record SemanticContextDto(
    IReadOnlyList<BusinessTermDto> RelevantTerms,
    IReadOnlyList<MetricDefinitionDto> RelevantMetrics,
    string FormattedPromptContext);
