using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Application.Features.DataSources.Dtos;
using IntelliQueryAI.Domain.Errors;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.SharedKernel.Results;

namespace IntelliQueryAI.Application.Features.DataSources.Queries;

public sealed record GetDataSourcesQuery(Guid WorkspaceId) : IQuery<Result<IReadOnlyList<DataSourceDto>>>;

public sealed class GetDataSourcesQueryHandler : IQueryHandler<GetDataSourcesQuery, Result<IReadOnlyList<DataSourceDto>>>
{
    private readonly IDataSourceStore _dataSourceStore;

    public GetDataSourcesQueryHandler(IDataSourceStore dataSourceStore)
    {
        _dataSourceStore = dataSourceStore;
    }

    public async Task<Result<IReadOnlyList<DataSourceDto>>> HandleAsync(GetDataSourcesQuery query, CancellationToken cancellationToken = default)
    {
        var list = await _dataSourceStore.GetByWorkspaceIdAsync(query.WorkspaceId, cancellationToken);
        var dtos = list.Select(ds => new DataSourceDto(
            ds.Id,
            ds.WorkspaceId,
            ds.Name,
            ds.DatabaseType.ToString(),
            ds.IsActive,
            ds.LastSyncAt,
            ds.SyncStatus.ToString(),
            ds.SyncErrorMessage,
            ds.CreatedAtUtc)).ToList();

        return Result.Success<IReadOnlyList<DataSourceDto>>(dtos);
    }
}

public sealed record GetDataSourceByIdQuery(Guid Id, Guid WorkspaceId) : IQuery<Result<DataSourceDto>>;

public sealed class GetDataSourceByIdQueryHandler : IQueryHandler<GetDataSourceByIdQuery, Result<DataSourceDto>>
{
    private readonly IDataSourceStore _dataSourceStore;

    public GetDataSourceByIdQueryHandler(IDataSourceStore dataSourceStore)
    {
        _dataSourceStore = dataSourceStore;
    }

    public async Task<Result<DataSourceDto>> HandleAsync(GetDataSourceByIdQuery query, CancellationToken cancellationToken = default)
    {
        var ds = await _dataSourceStore.GetByIdAsync(query.Id, cancellationToken);
        if (ds is null || ds.WorkspaceId != query.WorkspaceId)
        {
            return Result.Failure<DataSourceDto>(DataSourceErrors.NotFound);
        }

        return new DataSourceDto(
            ds.Id,
            ds.WorkspaceId,
            ds.Name,
            ds.DatabaseType.ToString(),
            ds.IsActive,
            ds.LastSyncAt,
            ds.SyncStatus.ToString(),
            ds.SyncErrorMessage,
            ds.CreatedAtUtc);
    }
}
