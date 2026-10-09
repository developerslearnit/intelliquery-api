using IntelliQueryAI.Domain.Entities.DataSources;

namespace IntelliQueryAI.Domain.Persistence;

public sealed record TableSummaryDto(
    Guid DataSourceId,
    string DataSourceName,
    string SchemaName,
    string TableName,
    string FriendlyTableName,
    int ColumnCount);

public sealed record ColumnMetadataDto(
    Guid Id,
    Guid DataSourceId,
    string SchemaName,
    string TableName,
    string ColumnName,
    string DataType,
    bool IsNullable,
    bool IsPrimaryKey,
    bool IsForeignKey,
    string? Description,
    string FriendlyTableName,
    string FriendlyColumnName,
    int OrdinalPosition);

public sealed record RelationshipDto(
    Guid Id,
    Guid DataSourceId,
    string ParentTable,
    string ParentColumn,
    string ChildTable,
    string ChildColumn,
    string? ConstraintName);

/// <summary>
/// Domain-owned contract for managing DataSource aggregate persistence.
/// </summary>
public interface IDataSourceStore
{
    Task<DataSource?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DataSource>> GetByWorkspaceIdAsync(Guid workspaceId, CancellationToken cancellationToken = default);
    Task<int> CountByWorkspaceIdAsync(Guid workspaceId, CancellationToken cancellationToken = default);
    Task AddAsync(DataSource dataSource, CancellationToken cancellationToken = default);
    Task DeleteAsync(DataSource dataSource, CancellationToken cancellationToken = default);
}

/// <summary>
/// Domain-owned contract for writing synchronized schema metadata.
/// </summary>
public interface ISchemaMetadataStore
{
    Task ReplaceSchemaAsync(
        Guid workspaceId,
        Guid dataSourceId,
        IReadOnlyList<SchemaMetadata> columns,
        IReadOnlyList<TableRelationship> relationships,
        IReadOnlyList<DatabaseRoutine> routines,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Dapper-optimized read store capable of querying millions of metadata records at high speed.
/// </summary>
public interface ISchemaReadStore
{
    Task<IReadOnlyList<TableSummaryDto>> GetTablesAsync(Guid workspaceId, Guid? dataSourceId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ColumnMetadataDto>> GetColumnsAsync(Guid workspaceId, Guid? dataSourceId, string? tableName, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RelationshipDto>> GetRelationshipsAsync(Guid workspaceId, Guid? dataSourceId, CancellationToken cancellationToken = default);
}
