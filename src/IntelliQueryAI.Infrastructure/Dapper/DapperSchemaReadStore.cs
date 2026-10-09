using System.Text;
using Dapper;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IntelliQueryAI.Infrastructure.Dapper;

/// <summary>
/// High-performance Dapper read store for projecting schema exploration views.
/// Employs raw SQL queries against PostgreSQL indexes with double-quoted identifiers
/// matching EF Core conventions, with resilient EF Core LINQ fallback.
/// </summary>
public sealed class DapperSchemaReadStore : ISchemaReadStore
{
    private readonly ISqlConnectionFactory _connectionFactory;
    private readonly IntelliQueryDbContext _context;

    public DapperSchemaReadStore(
        ISqlConnectionFactory connectionFactory,
        IntelliQueryDbContext context)
    {
        _connectionFactory = connectionFactory;
        _context = context;
    }

    public async Task<IReadOnlyList<TableSummaryDto>> GetTablesAsync(
        Guid workspaceId,
        Guid? dataSourceId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

            var sql = new StringBuilder(@"
                SELECT 
                    s.""DataSourceId"" AS DataSourceId,
                    d.""Name"" AS DataSourceName,
                    s.""SchemaName"" AS SchemaName,
                    s.""TableName"" AS TableName,
                    s.""FriendlyTableName"" AS FriendlyTableName,
                    COUNT(s.""Id"") AS ColumnCount
                FROM schema_metadata s
                JOIN data_sources d ON s.""DataSourceId"" = d.""Id""
                WHERE d.""IsActive"" = true AND LOWER(s.""TableName"") <> '__efmigrationshistory' ");

            var parameters = new DynamicParameters();
            if (workspaceId != Guid.Empty)
            {
                sql.Append(@" AND s.""WorkspaceId"" = @WorkspaceId");
                parameters.Add("WorkspaceId", workspaceId);
            }
            if (dataSourceId.HasValue && dataSourceId.Value != Guid.Empty)
            {
                sql.Append(@" AND s.""DataSourceId"" = @DataSourceId");
                parameters.Add("DataSourceId", dataSourceId.Value);
            }

            sql.Append(@" GROUP BY s.""DataSourceId"", d.""Name"", s.""SchemaName"", s.""TableName"", s.""FriendlyTableName""
                          ORDER BY s.""TableName"";");

            var result = await conn.QueryAsync<TableSummaryDto>(
                new CommandDefinition(sql.ToString(), parameters, cancellationToken: cancellationToken));

            var list = result.AsList();
            if (list.Count > 0)
            {
                return list;
            }
        }
        catch
        {
            // Fall back to EF Core LINQ
        }

        // Resilient EF Core fallback
        var metaQuery = _context.SchemaMetadata.AsNoTracking()
            .Where(s => !s.TableName.ToLower().Equals("__efmigrationshistory"));
        if (workspaceId != Guid.Empty)
            metaQuery = metaQuery.Where(s => s.WorkspaceId == workspaceId);
        if (dataSourceId.HasValue && dataSourceId.Value != Guid.Empty)
            metaQuery = metaQuery.Where(s => s.DataSourceId == dataSourceId.Value);

        var grouped = await metaQuery
            .GroupBy(s => new { s.DataSourceId, s.SchemaName, s.TableName, s.FriendlyTableName })
            .Select(g => new
            {
                g.Key.DataSourceId,
                g.Key.SchemaName,
                g.Key.TableName,
                g.Key.FriendlyTableName,
                ColumnCount = g.Count()
            })
            .ToListAsync(cancellationToken);

        var dsIds = grouped.Select(g => g.DataSourceId).Distinct().ToList();
        var dsNames = await _context.DataSources
            .Where(d => dsIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => d.Name, cancellationToken);

        return grouped.Select(g => new TableSummaryDto(
            g.DataSourceId,
            dsNames.TryGetValue(g.DataSourceId, out var name) ? name : "Data Source",
            g.SchemaName,
            g.TableName,
            g.FriendlyTableName,
            g.ColumnCount)).OrderBy(t => t.TableName).ToList();
    }

    public async Task<IReadOnlyList<ColumnMetadataDto>> GetColumnsAsync(
        Guid workspaceId,
        Guid? dataSourceId,
        string? tableName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

            var sql = new StringBuilder(@"
                SELECT 
                    s.""Id"" AS Id,
                    s.""DataSourceId"" AS DataSourceId,
                    s.""SchemaName"" AS SchemaName,
                    s.""TableName"" AS TableName,
                    s.""ColumnName"" AS ColumnName,
                    s.""DataType"" AS DataType,
                    s.""IsNullable"" AS IsNullable,
                    s.""IsPrimaryKey"" AS IsPrimaryKey,
                    s.""IsForeignKey"" AS IsForeignKey,
                    s.""Description"" AS Description,
                    s.""FriendlyTableName"" AS FriendlyTableName,
                    s.""FriendlyColumnName"" AS FriendlyColumnName,
                    s.""OrdinalPosition"" AS OrdinalPosition
                FROM schema_metadata s
                WHERE LOWER(s.""TableName"") <> '__efmigrationshistory' ");

            var parameters = new DynamicParameters();
            if (workspaceId != Guid.Empty)
            {
                sql.Append(@" AND s.""WorkspaceId"" = @WorkspaceId");
                parameters.Add("WorkspaceId", workspaceId);
            }
            if (dataSourceId.HasValue && dataSourceId.Value != Guid.Empty)
            {
                sql.Append(@" AND s.""DataSourceId"" = @DataSourceId");
                parameters.Add("DataSourceId", dataSourceId.Value);
            }
            if (!string.IsNullOrWhiteSpace(tableName))
            {
                sql.Append(@" AND s.""TableName"" = @TableName");
                parameters.Add("TableName", tableName);
            }
            sql.Append(@" ORDER BY s.""TableName"", s.""OrdinalPosition"";");

            var result = await conn.QueryAsync<ColumnMetadataDto>(
                new CommandDefinition(sql.ToString(), parameters, cancellationToken: cancellationToken));

            var list = result.AsList();
            if (list.Count > 0)
            {
                return list;
            }
        }
        catch
        {
            // Fall back to EF Core LINQ
        }

        // Resilient EF Core fallback
        var query = _context.SchemaMetadata.AsNoTracking()
            .Where(s => !s.TableName.ToLower().Equals("__efmigrationshistory"));
        if (workspaceId != Guid.Empty)
            query = query.Where(s => s.WorkspaceId == workspaceId);
        if (dataSourceId.HasValue && dataSourceId.Value != Guid.Empty)
            query = query.Where(s => s.DataSourceId == dataSourceId.Value);
        if (!string.IsNullOrWhiteSpace(tableName))
            query = query.Where(s => s.TableName == tableName);

        return await query
            .OrderBy(s => s.TableName)
            .ThenBy(s => s.OrdinalPosition)
            .Select(s => new ColumnMetadataDto(
                s.Id,
                s.DataSourceId,
                s.SchemaName,
                s.TableName,
                s.ColumnName,
                s.DataType,
                s.IsNullable,
                s.IsPrimaryKey,
                s.IsForeignKey,
                s.Description,
                s.FriendlyTableName,
                s.FriendlyColumnName,
                s.OrdinalPosition))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RelationshipDto>> GetRelationshipsAsync(
        Guid workspaceId,
        Guid? dataSourceId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

            var sql = new StringBuilder(@"
                SELECT 
                    r.""Id"" AS Id,
                    r.""DataSourceId"" AS DataSourceId,
                    r.""ParentTable"" AS ParentTable,
                    r.""ParentColumn"" AS ParentColumn,
                    r.""ChildTable"" AS ChildTable,
                    r.""ChildColumn"" AS ChildColumn,
                    r.""ConstraintName"" AS ConstraintName
                FROM table_relationships r
                WHERE 1=1 ");

            var parameters = new DynamicParameters();
            if (workspaceId != Guid.Empty)
            {
                sql.Append(@" AND r.""WorkspaceId"" = @WorkspaceId");
                parameters.Add("WorkspaceId", workspaceId);
            }
            if (dataSourceId.HasValue && dataSourceId.Value != Guid.Empty)
            {
                sql.Append(@" AND r.""DataSourceId"" = @DataSourceId");
                parameters.Add("DataSourceId", dataSourceId.Value);
            }

            sql.Append(@" ORDER BY r.""ParentTable"", r.""ChildTable"";");

            var result = await conn.QueryAsync<RelationshipDto>(
                new CommandDefinition(sql.ToString(), parameters, cancellationToken: cancellationToken));

            var list = result.AsList();
            if (list.Count > 0)
            {
                return list;
            }
        }
        catch
        {
            // Fall back to EF Core LINQ
        }

        // Resilient EF Core fallback
        var relQuery = _context.TableRelationships.AsNoTracking();
        if (workspaceId != Guid.Empty)
            relQuery = relQuery.Where(r => r.WorkspaceId == workspaceId);
        if (dataSourceId.HasValue && dataSourceId.Value != Guid.Empty)
            relQuery = relQuery.Where(r => r.DataSourceId == dataSourceId.Value);

        return await relQuery
            .OrderBy(r => r.ParentTable)
            .ThenBy(r => r.ChildTable)
            .Select(r => new RelationshipDto(
                r.Id,
                r.DataSourceId,
                r.ParentTable,
                r.ParentColumn,
                r.ChildTable,
                r.ChildColumn,
                r.ConstraintName))
            .ToListAsync(cancellationToken);
    }
}
