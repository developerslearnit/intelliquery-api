using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Application.Features.Ai.Dtos;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.SharedKernel.Results;

namespace IntelliQueryAI.Application.Features.Ai.Queries;

public sealed record GetSavedReportsQuery(Guid WorkspaceId) : IQuery<Result<IReadOnlyList<SavedReportDto>>>;

public sealed class GetSavedReportsQueryHandler : IQueryHandler<GetSavedReportsQuery, Result<IReadOnlyList<SavedReportDto>>>
{
    private readonly ISavedReportStore _reportStore;

    public GetSavedReportsQueryHandler(ISavedReportStore reportStore)
    {
        _reportStore = reportStore;
    }

    public async Task<Result<IReadOnlyList<SavedReportDto>>> HandleAsync(
        GetSavedReportsQuery query,
        CancellationToken cancellationToken = default)
    {
        var reports = await _reportStore.GetByWorkspaceIdAsync(query.WorkspaceId, cancellationToken);
        var dtos = reports.Select(r => new SavedReportDto(
            r.Id,
            r.WorkspaceId,
            r.UserId,
            r.Title,
            r.Description,
            r.Question,
            r.SqlQuery,
            r.ChartConfigurationJson,
            r.InsightsJson,
            r.CreatedAtUtc,
            r.SqlQuery,
            ExtractChartType(r.ChartConfigurationJson),
            r.CreatedAtUtc)).ToList();

        return Result.Success<IReadOnlyList<SavedReportDto>>(dtos);
    }

    private static string? ExtractChartType(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("chartType", out var prop))
                return prop.GetString();
            if (doc.RootElement.TryGetProperty("ChartType", out var propUpper))
                return propUpper.GetString();
        }
        catch { }
        return null;
    }
}

public sealed record GetQueryHistoryQuery(Guid WorkspaceId, int Limit = 50) : IQuery<Result<IReadOnlyList<QueryHistoryDto>>>;

public sealed class GetQueryHistoryQueryHandler : IQueryHandler<GetQueryHistoryQuery, Result<IReadOnlyList<QueryHistoryDto>>>
{
    private readonly IQueryHistoryStore _historyStore;

    public GetQueryHistoryQueryHandler(IQueryHistoryStore historyStore)
    {
        _historyStore = historyStore;
    }

    public async Task<Result<IReadOnlyList<QueryHistoryDto>>> HandleAsync(
        GetQueryHistoryQuery query,
        CancellationToken cancellationToken = default)
    {
        var histories = await _historyStore.GetByWorkspaceIdAsync(query.WorkspaceId, query.Limit, cancellationToken);
        var dtos = histories.Select(h => new QueryHistoryDto(
            h.Id,
            h.WorkspaceId,
            h.DataSourceId,
            h.UserId,
            h.Question,
            h.GeneratedSql,
            h.ExecutionTimeMs,
            h.RowCount,
            h.IsSuccessful,
            h.ErrorMessage,
            h.ConfidenceScore,
            h.ExecutedAtUtc)).ToList();

        return Result.Success<IReadOnlyList<QueryHistoryDto>>(dtos);
    }
}

public sealed record GetPagedQueryHistoryQuery(
    Guid WorkspaceId,
    int Page = 1,
    int PageSize = 10) : IQuery<Result<PagedResult<QueryHistoryDto>>>;

public sealed class GetPagedQueryHistoryQueryHandler : IQueryHandler<GetPagedQueryHistoryQuery, Result<PagedResult<QueryHistoryDto>>>
{
    private readonly IQueryHistoryStore _historyStore;

    public GetPagedQueryHistoryQueryHandler(IQueryHistoryStore historyStore)
    {
        _historyStore = historyStore;
    }

    public async Task<Result<PagedResult<QueryHistoryDto>>> HandleAsync(
        GetPagedQueryHistoryQuery query,
        CancellationToken cancellationToken = default)
    {
        var (histories, totalCount) = await _historyStore.GetPagedByWorkspaceIdAsync(
            query.WorkspaceId,
            query.Page,
            query.PageSize,
            cancellationToken);

        var dtos = histories.Select(h => new QueryHistoryDto(
            h.Id,
            h.WorkspaceId,
            h.DataSourceId,
            h.UserId,
            h.Question,
            h.GeneratedSql,
            h.ExecutionTimeMs,
            h.RowCount,
            h.IsSuccessful,
            h.ErrorMessage,
            h.ConfidenceScore,
            h.ExecutedAtUtc)).ToList();

        var pagedResult = PagedResult<QueryHistoryDto>.Create(dtos, totalCount, query.Page, query.PageSize);
        return Result.Success(pagedResult);
    }
}
