using System.Text;
using System.Text.RegularExpressions;
using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Application.Features.BusinessKnowledge.Dtos;
using IntelliQueryAI.Domain.Errors;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.SharedKernel.Results;

namespace IntelliQueryAI.Application.Features.BusinessKnowledge.Queries;

// -------------------------------------------------------------------------------------------------
// 1. Get Business Terms Query
// -------------------------------------------------------------------------------------------------
public sealed record GetBusinessTermsQuery(
    Guid WorkspaceId,
    string? Search = null,
    string? Category = null,
    bool? IsActive = null) : IQuery<Result<IReadOnlyList<BusinessTermDto>>>;

public sealed class GetBusinessTermsQueryHandler : IQueryHandler<GetBusinessTermsQuery, Result<IReadOnlyList<BusinessTermDto>>>
{
    private readonly IBusinessTermStore _store;

    public GetBusinessTermsQueryHandler(IBusinessTermStore store)
    {
        _store = store;
    }

    public async Task<Result<IReadOnlyList<BusinessTermDto>>> HandleAsync(GetBusinessTermsQuery query, CancellationToken cancellationToken = default)
    {
        var terms = await _store.GetByWorkspaceIdAsync(query.WorkspaceId, query.Search, query.Category, query.IsActive, cancellationToken);
        var dtos = terms.Select(BusinessTermDto.FromDomain).ToList();
        return Result.Success<IReadOnlyList<BusinessTermDto>>(dtos);
    }
}

// -------------------------------------------------------------------------------------------------
// 2. Get Business Term By Id Query
// -------------------------------------------------------------------------------------------------
public sealed record GetBusinessTermByIdQuery(Guid WorkspaceId, Guid Id) : IQuery<Result<BusinessTermDto>>;

public sealed class GetBusinessTermByIdQueryHandler : IQueryHandler<GetBusinessTermByIdQuery, Result<BusinessTermDto>>
{
    private readonly IBusinessTermStore _store;

    public GetBusinessTermByIdQueryHandler(IBusinessTermStore store)
    {
        _store = store;
    }

    public async Task<Result<BusinessTermDto>> HandleAsync(GetBusinessTermByIdQuery query, CancellationToken cancellationToken = default)
    {
        var term = await _store.GetByIdAsync(query.Id, cancellationToken);
        if (term is null || term.WorkspaceId != query.WorkspaceId)
        {
            return Result.Failure<BusinessTermDto>(Error.NotFound("BusinessTerm.NotFound", "The business term was not found in this workspace."));
        }

        return Result.Success(BusinessTermDto.FromDomain(term));
    }
}

// -------------------------------------------------------------------------------------------------
// 3. Get Metric Definitions Query
// -------------------------------------------------------------------------------------------------
public sealed record GetMetricDefinitionsQuery(
    Guid WorkspaceId,
    string? Search = null,
    bool? IsActive = null) : IQuery<Result<IReadOnlyList<MetricDefinitionDto>>>;

public sealed class GetMetricDefinitionsQueryHandler : IQueryHandler<GetMetricDefinitionsQuery, Result<IReadOnlyList<MetricDefinitionDto>>>
{
    private readonly IMetricStore _store;

    public GetMetricDefinitionsQueryHandler(IMetricStore store)
    {
        _store = store;
    }

    public async Task<Result<IReadOnlyList<MetricDefinitionDto>>> HandleAsync(GetMetricDefinitionsQuery query, CancellationToken cancellationToken = default)
    {
        var metrics = await _store.GetByWorkspaceIdAsync(query.WorkspaceId, query.Search, query.IsActive, cancellationToken);
        var dtos = metrics.Select(MetricDefinitionDto.FromDomain).ToList();
        return Result.Success<IReadOnlyList<MetricDefinitionDto>>(dtos);
    }
}

// -------------------------------------------------------------------------------------------------
// 4. Get Metric Definition By Id Query
// -------------------------------------------------------------------------------------------------
public sealed record GetMetricDefinitionByIdQuery(Guid WorkspaceId, Guid Id) : IQuery<Result<MetricDefinitionDto>>;

public sealed class GetMetricDefinitionByIdQueryHandler : IQueryHandler<GetMetricDefinitionByIdQuery, Result<MetricDefinitionDto>>
{
    private readonly IMetricStore _store;

    public GetMetricDefinitionByIdQueryHandler(IMetricStore store)
    {
        _store = store;
    }

    public async Task<Result<MetricDefinitionDto>> HandleAsync(GetMetricDefinitionByIdQuery query, CancellationToken cancellationToken = default)
    {
        var metric = await _store.GetByIdAsync(query.Id, cancellationToken);
        if (metric is null || metric.WorkspaceId != query.WorkspaceId)
        {
            return Result.Failure<MetricDefinitionDto>(Error.NotFound("MetricDefinition.NotFound", "The metric definition was not found in this workspace."));
        }

        return Result.Success(MetricDefinitionDto.FromDomain(metric));
    }
}

// -------------------------------------------------------------------------------------------------
// 5. Resolve Semantic Context Query (AI Semantic Enrichment)
// -------------------------------------------------------------------------------------------------
public sealed record ResolveSemanticContextQuery(Guid WorkspaceId, string UserPrompt) : IQuery<Result<SemanticContextDto>>;

public sealed class ResolveSemanticContextQueryHandler : IQueryHandler<ResolveSemanticContextQuery, Result<SemanticContextDto>>
{
    private readonly IBusinessTermStore _termStore;
    private readonly IMetricStore _metricStore;

    public ResolveSemanticContextQueryHandler(IBusinessTermStore termStore, IMetricStore metricStore)
    {
        _termStore = termStore;
        _metricStore = metricStore;
    }

    public async Task<Result<SemanticContextDto>> HandleAsync(ResolveSemanticContextQuery query, CancellationToken cancellationToken = default)
    {
        var tokens = ExtractSearchTokens(query.UserPrompt);

        var terms = await _termStore.SearchRelevantTermsAsync(query.WorkspaceId, tokens, cancellationToken);
        var metrics = await _metricStore.SearchRelevantMetricsAsync(query.WorkspaceId, tokens, cancellationToken);

        var termDtos = terms.Select(BusinessTermDto.FromDomain).ToList();
        var metricDtos = metrics.Select(MetricDefinitionDto.FromDomain).ToList();

        var sb = new StringBuilder();

        if (termDtos.Count > 0 || metricDtos.Count > 0)
        {
            sb.AppendLine("BUSINESS KNOWLEDGE & SEMANTIC DEFINITIONS:");

            foreach (var term in termDtos)
            {
                sb.Append($"- Term '{term.Name}' ({term.Category}): {term.Definition}");
                if (!string.IsNullOrWhiteSpace(term.TargetTable))
                {
                    sb.Append($" [Table: {term.TargetTable}");
                    if (!string.IsNullOrWhiteSpace(term.TargetColumn))
                    {
                        sb.Append($", Column: {term.TargetColumn}");
                    }
                    sb.Append(']');
                }
                if (!string.IsNullOrWhiteSpace(term.SqlCalculationRule))
                {
                    sb.Append($" [Rule: {term.SqlCalculationRule}]");
                }
                sb.AppendLine();
            }

            foreach (var m in metricDtos)
            {
                sb.Append($"- Metric '{m.Name}' (Unit: {m.Unit}, Agg: {m.AggregationType}): {m.Description}. Formula: {m.CalculationFormula}");
                if (!string.IsNullOrWhiteSpace(m.FilterExpression))
                {
                    sb.Append($" [Filter: {m.FilterExpression}]");
                }
                if (!string.IsNullOrWhiteSpace(m.TargetTable))
                {
                    sb.Append($" [Table: {m.TargetTable}]");
                }
                sb.AppendLine();
            }
        }

        var dto = new SemanticContextDto(termDtos, metricDtos, sb.ToString().TrimEnd());
        return Result.Success(dto);
    }

    private static List<string> ExtractSearchTokens(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt)) return new List<string>();

        // Remove punctuation and split words
        var cleaned = Regex.Replace(prompt, @"[^\w\s]", " ");
        var words = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 3)
            .Select(w => w.ToLowerInvariant())
            .Distinct()
            .ToList();

        // Also extract 2-word n-grams for compound phrases (e.g. "active customer", "total revenue")
        var rawWords = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var ngrams = new List<string>();
        for (int i = 0; i < rawWords.Length - 1; i++)
        {
            if (rawWords[i].Length >= 2 && rawWords[i + 1].Length >= 2)
            {
                ngrams.Add($"{rawWords[i]} {rawWords[i + 1]}".ToLowerInvariant());
            }
        }

        words.AddRange(ngrams);
        return words.Distinct().ToList();
    }
}
