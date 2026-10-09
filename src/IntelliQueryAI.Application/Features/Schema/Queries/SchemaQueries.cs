using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.SharedKernel.Results;

namespace IntelliQueryAI.Application.Features.Schema.Queries;

/// <summary>
/// Query retrieving high-level table summaries with column counts and human-friendly aliases.
/// Consumes the Dapper read store for sub-millisecond query performance.
/// </summary>
public sealed record GetSchemaTablesQuery(
    Guid WorkspaceId,
    Guid? DataSourceId = null) : IQuery<Result<IReadOnlyList<TableSummaryDto>>>;

public sealed class GetSchemaTablesQueryHandler : IQueryHandler<GetSchemaTablesQuery, Result<IReadOnlyList<TableSummaryDto>>>
{
    private readonly ISchemaReadStore _readStore;

    public GetSchemaTablesQueryHandler(ISchemaReadStore readStore)
    {
        _readStore = readStore;
    }

    public async Task<Result<IReadOnlyList<TableSummaryDto>>> HandleAsync(GetSchemaTablesQuery query, CancellationToken cancellationToken = default)
    {
        var tables = await _readStore.GetTablesAsync(query.WorkspaceId, query.DataSourceId, cancellationToken);
        return Result.Success(tables);
    }
}

/// <summary>
/// Query retrieving foreign key relationships for schema relationship graphs.
/// </summary>
public sealed record GetSchemaRelationshipsQuery(
    Guid WorkspaceId,
    Guid? DataSourceId = null) : IQuery<Result<IReadOnlyList<RelationshipDto>>>;

public sealed class GetSchemaRelationshipsQueryHandler : IQueryHandler<GetSchemaRelationshipsQuery, Result<IReadOnlyList<RelationshipDto>>>
{
    private readonly ISchemaReadStore _readStore;

    public GetSchemaRelationshipsQueryHandler(ISchemaReadStore readStore)
    {
        _readStore = readStore;
    }

    public async Task<Result<IReadOnlyList<RelationshipDto>>> HandleAsync(GetSchemaRelationshipsQuery query, CancellationToken cancellationToken = default)
    {
        var relationships = await _readStore.GetRelationshipsAsync(query.WorkspaceId, query.DataSourceId, cancellationToken);
        return Result.Success(relationships);
    }
}

/// <summary>
/// Query retrieving detailed column metadata and descriptions.
/// </summary>
public sealed record GetFullSchemaQuery(
    Guid WorkspaceId,
    Guid? DataSourceId = null,
    string? TableName = null) : IQuery<Result<IReadOnlyList<ColumnMetadataDto>>>;

public sealed class GetFullSchemaQueryHandler : IQueryHandler<GetFullSchemaQuery, Result<IReadOnlyList<ColumnMetadataDto>>>
{
    private readonly ISchemaReadStore _readStore;

    public GetFullSchemaQueryHandler(ISchemaReadStore readStore)
    {
        _readStore = readStore;
    }

    public async Task<Result<IReadOnlyList<ColumnMetadataDto>>> HandleAsync(GetFullSchemaQuery query, CancellationToken cancellationToken = default)
    {
        var columns = await _readStore.GetColumnsAsync(query.WorkspaceId, query.DataSourceId, query.TableName, cancellationToken);
        return Result.Success(columns);
    }
}
