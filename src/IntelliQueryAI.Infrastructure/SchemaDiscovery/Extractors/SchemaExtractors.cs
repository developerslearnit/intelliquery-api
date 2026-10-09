using System.Data;
using System.Data.Common;
using Dapper;
using IntelliQueryAI.Domain.Entities.DataSources;
using IntelliQueryAI.Domain.Services;
using IntelliQueryAI.Infrastructure.SchemaDiscovery.Aliasing;
using Microsoft.Data.SqlClient;
using MySqlConnector;
using Npgsql;

namespace IntelliQueryAI.Infrastructure.SchemaDiscovery.Extractors;

public interface ISchemaExtractor
{
    Task<bool> TestConnectionAsync(string connectionString, CancellationToken cancellationToken = default);

    Task<DiscoveredSchemaResult> ExtractSchemaAsync(
        Guid workspaceId,
        Guid dataSourceId,
        string connectionString,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Extracts schema metadata from PostgreSQL databases using information_schema and pg_catalog.
/// </summary>
public sealed class PostgreSqlSchemaExtractor : ISchemaExtractor
{
    public async Task<bool> TestConnectionAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT 1;";
            cmd.CommandTimeout = 5;
            await cmd.ExecuteScalarAsync(cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<DiscoveredSchemaResult> ExtractSchemaAsync(
        Guid workspaceId,
        Guid dataSourceId,
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);

        // 1. Discover Columns
        const string columnsSql = @"
            SELECT 
                c.table_schema AS SchemaName,
                c.table_name AS TableName,
                c.column_name AS ColumnName,
                c.data_type AS DataType,
                (c.is_nullable = 'YES') AS IsNullable,
                c.ordinal_position AS OrdinalPosition,
                CASE WHEN pk.column_name IS NOT NULL THEN true ELSE false END AS IsPrimaryKey,
                CASE WHEN fk.column_name IS NOT NULL THEN true ELSE false END AS IsForeignKey
            FROM information_schema.columns c
            JOIN information_schema.tables t 
                ON c.table_schema = t.table_schema AND c.table_name = t.table_name
            LEFT JOIN (
                SELECT DISTINCT tc.table_schema, tc.table_name, kcu.column_name
                FROM information_schema.table_constraints tc
                JOIN information_schema.key_column_usage kcu
                    ON tc.constraint_name = kcu.constraint_name AND tc.table_schema = kcu.table_schema
                WHERE tc.constraint_type = 'PRIMARY KEY'
            ) pk ON c.table_schema = pk.table_schema AND c.table_name = pk.table_name AND c.column_name = pk.column_name
            LEFT JOIN (
                SELECT DISTINCT tc.table_schema, tc.table_name, kcu.column_name
                FROM information_schema.table_constraints tc
                JOIN information_schema.key_column_usage kcu
                    ON tc.constraint_name = kcu.constraint_name AND tc.table_schema = kcu.table_schema
                WHERE tc.constraint_type = 'FOREIGN KEY'
            ) fk ON c.table_schema = fk.table_schema AND c.table_name = fk.table_name AND c.column_name = fk.column_name
            WHERE c.table_schema NOT IN ('pg_catalog', 'information_schema')
              AND t.table_type IN ('BASE TABLE', 'VIEW')
              AND LOWER(c.table_name) <> '__efmigrationshistory'
            ORDER BY c.table_schema, c.table_name, c.ordinal_position;";

        var rawColumns = await conn.QueryAsync(new CommandDefinition(columnsSql, cancellationToken: cancellationToken));
        var columns = new List<SchemaMetadata>();

        foreach (var col in rawColumns)
        {
            string tableName = col.tablename;
            string colName = col.columnname;
            string schemaName = col.schemaname;
            string dataType = col.datatype;
            bool isNullable = Convert.ToBoolean(col.isnullable);
            bool isPrimaryKey = Convert.ToBoolean(col.isprimarykey);
            bool isForeignKey = Convert.ToBoolean(col.isforeignkey);
            int ordPos = Convert.ToInt32(col.ordinalposition);

            string friendlyTable = BusinessNameGenerator.ToFriendlyName(tableName);
            string friendlyCol = BusinessNameGenerator.ToFriendlyName(colName);

            columns.Add(SchemaMetadata.Create(
                workspaceId,
                dataSourceId,
                schemaName,
                tableName,
                colName,
                dataType,
                isNullable,
                isPrimaryKey,
                isForeignKey,
                description: null,
                friendlyTable,
                friendlyCol,
                ordPos));
        }

        // 2. Discover Foreign Key Relationships
        const string fkSql = @"
            SELECT
                tc.constraint_name AS ConstraintName,
                ccu.table_name AS ParentTable,
                ccu.column_name AS ParentColumn,
                tc.table_name AS ChildTable,
                kcu.column_name AS ChildColumn
            FROM information_schema.table_constraints tc
            JOIN information_schema.key_column_usage kcu
                ON tc.constraint_name = kcu.constraint_name AND tc.table_schema = kcu.table_schema
            JOIN information_schema.constraint_column_usage ccu
                ON ccu.constraint_name = tc.constraint_name AND ccu.table_schema = tc.table_schema
            WHERE tc.constraint_type = 'FOREIGN KEY'
              AND tc.table_schema NOT IN ('pg_catalog', 'information_schema')
              AND LOWER(ccu.table_name) <> '__efmigrationshistory'
              AND LOWER(tc.table_name) <> '__efmigrationshistory';";

        var rawFks = await conn.QueryAsync(new CommandDefinition(fkSql, cancellationToken: cancellationToken));
        var relationships = new List<TableRelationship>();

        foreach (var fk in rawFks)
        {
            relationships.Add(TableRelationship.Create(
                workspaceId,
                dataSourceId,
                fk.parenttable,
                fk.parentcolumn,
                fk.childtable,
                fk.childcolumn,
                fk.constraintname));
        }

        // 3. Discover Views and Stored Procedures
        const string routinesSql = @"
            SELECT table_name AS RoutineName, 'VIEW' AS RoutineType, view_definition AS Definition
            FROM information_schema.views
            WHERE table_schema NOT IN ('pg_catalog', 'information_schema')
              AND LOWER(table_name) <> '__efmigrationshistory'
            UNION ALL
            SELECT routine_name AS RoutineName, routine_type AS RoutineType, routine_definition AS Definition
            FROM information_schema.routines
            WHERE routine_schema NOT IN ('pg_catalog', 'information_schema')
              AND LOWER(routine_name) <> '__efmigrationshistory';";

        var rawRoutines = await conn.QueryAsync(new CommandDefinition(routinesSql, cancellationToken: cancellationToken));
        var routines = new List<DatabaseRoutine>();

        foreach (var r in rawRoutines)
        {
            string name = r.routinename;
            string type = r.routinetype;
            string? def = r.definition;
            routines.Add(DatabaseRoutine.Create(
                workspaceId,
                dataSourceId,
                name,
                type,
                def,
                BusinessNameGenerator.ToFriendlyName(name)));
        }

        return new DiscoveredSchemaResult(columns, relationships, routines);
    }
}

/// <summary>
/// Extracts schema metadata from Microsoft SQL Server using sys catalog views.
/// </summary>
public sealed class SqlServerSchemaExtractor : ISchemaExtractor
{
    public async Task<bool> TestConnectionAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT 1;";
            cmd.CommandTimeout = 5;
            await cmd.ExecuteScalarAsync(cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<DiscoveredSchemaResult> ExtractSchemaAsync(
        Guid workspaceId,
        Guid dataSourceId,
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);

        const string columnsSql = @"
            SELECT 
                ISNULL(SCHEMA_NAME(t.schema_id), 'dbo') AS SchemaName,
                t.name AS TableName,
                c.name AS ColumnName,
                ISNULL(TYPE_NAME(c.user_type_id), TYPE_NAME(c.system_type_id)) AS DataType,
                c.is_nullable AS IsNullable,
                c.column_id AS OrdinalPosition,
                CAST(CASE WHEN pk.column_id IS NOT NULL THEN 1 ELSE 0 END AS BIT) AS IsPrimaryKey,
                CAST(CASE WHEN fk.parent_column_id IS NOT NULL THEN 1 ELSE 0 END AS BIT) AS IsForeignKey
            FROM sys.objects t
            JOIN sys.columns c ON t.object_id = c.object_id
            LEFT JOIN (
                SELECT DISTINCT ic.object_id, ic.column_id
                FROM sys.indexes i
                JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
                WHERE i.is_primary_key = 1
            ) pk ON c.object_id = pk.object_id AND c.column_id = pk.column_id
            LEFT JOIN (
                SELECT DISTINCT parent_object_id, parent_column_id
                FROM sys.foreign_key_columns
            ) fk ON c.object_id = fk.parent_object_id AND c.column_id = fk.parent_column_id
            WHERE ISNULL(SCHEMA_NAME(t.schema_id), 'dbo') NOT IN ('sys', 'information_schema')
              AND (t.is_ms_shipped = 0 OR t.is_ms_shipped IS NULL)
              AND t.type IN ('U', 'V')
              AND LOWER(t.name) <> '__efmigrationshistory'
            ORDER BY SchemaName, TableName, OrdinalPosition;";

        var rawColumns = await conn.QueryAsync<SqlServerColumnRow>(new CommandDefinition(columnsSql, cancellationToken: cancellationToken));
        var columns = new List<SchemaMetadata>();

        foreach (var col in rawColumns)
        {
            if (string.IsNullOrWhiteSpace(col.TableName) || string.IsNullOrWhiteSpace(col.ColumnName))
                continue;

            columns.Add(SchemaMetadata.Create(
                workspaceId,
                dataSourceId,
                col.SchemaName,
                col.TableName,
                col.ColumnName,
                col.DataType,
                col.IsNullable,
                col.IsPrimaryKey,
                col.IsForeignKey,
                null,
                BusinessNameGenerator.ToFriendlyName(col.TableName),
                BusinessNameGenerator.ToFriendlyName(col.ColumnName),
                col.OrdinalPosition));
        }

        const string fkSql = @"
            SELECT 
                fk.name AS ConstraintName,
                OBJECT_NAME(fkc.referenced_object_id) AS ParentTable,
                COL_NAME(fkc.referenced_object_id, fkc.referenced_column_id) AS ParentColumn,
                OBJECT_NAME(fkc.parent_object_id) AS ChildTable,
                COL_NAME(fkc.parent_object_id, fkc.parent_column_id) AS ChildColumn
            FROM sys.foreign_keys fk
            JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
            WHERE fkc.referenced_object_id IS NOT NULL AND fkc.parent_object_id IS NOT NULL
              AND LOWER(OBJECT_NAME(fkc.referenced_object_id)) <> '__efmigrationshistory'
              AND LOWER(OBJECT_NAME(fkc.parent_object_id)) <> '__efmigrationshistory';";

        var rawFks = await conn.QueryAsync<SqlServerFkRow>(new CommandDefinition(fkSql, cancellationToken: cancellationToken));
        var relationships = new List<TableRelationship>();

        foreach (var fk in rawFks)
        {
            if (string.IsNullOrWhiteSpace(fk.ParentTable) || string.IsNullOrWhiteSpace(fk.ParentColumn) ||
                string.IsNullOrWhiteSpace(fk.ChildTable) || string.IsNullOrWhiteSpace(fk.ChildColumn))
                continue;

            relationships.Add(TableRelationship.Create(
                workspaceId,
                dataSourceId,
                fk.ParentTable,
                fk.ParentColumn,
                fk.ChildTable,
                fk.ChildColumn,
                fk.ConstraintName));
        }

        const string routinesSql = @"
            SELECT name AS RoutineName, 'VIEW' AS RoutineType, OBJECT_DEFINITION(object_id) AS Definition
            FROM sys.views 
            WHERE (is_ms_shipped = 0 OR is_ms_shipped IS NULL)
              AND ISNULL(SCHEMA_NAME(schema_id), 'dbo') NOT IN ('sys', 'information_schema')
              AND LOWER(name) <> '__efmigrationshistory'
            UNION ALL
            SELECT name AS RoutineName, 'PROCEDURE' AS RoutineType, OBJECT_DEFINITION(object_id) AS Definition
            FROM sys.procedures 
            WHERE (is_ms_shipped = 0 OR is_ms_shipped IS NULL)
              AND ISNULL(SCHEMA_NAME(schema_id), 'dbo') NOT IN ('sys', 'information_schema')
              AND LOWER(name) <> '__efmigrationshistory';";

        var rawRoutines = await conn.QueryAsync<SqlServerRoutineRow>(new CommandDefinition(routinesSql, cancellationToken: cancellationToken));
        var routines = new List<DatabaseRoutine>();

        foreach (var r in rawRoutines)
        {
            if (string.IsNullOrWhiteSpace(r.RoutineName))
                continue;

            routines.Add(DatabaseRoutine.Create(
                workspaceId,
                dataSourceId,
                r.RoutineName,
                r.RoutineType,
                r.Definition,
                BusinessNameGenerator.ToFriendlyName(r.RoutineName)));
        }

        return new DiscoveredSchemaResult(columns, relationships, routines);
    }

    private sealed class SqlServerColumnRow
    {
        public string SchemaName { get; set; } = "dbo";
        public string TableName { get; set; } = string.Empty;
        public string ColumnName { get; set; } = string.Empty;
        public string DataType { get; set; } = "varchar";
        public bool IsNullable { get; set; }
        public int OrdinalPosition { get; set; }
        public bool IsPrimaryKey { get; set; }
        public bool IsForeignKey { get; set; }
    }

    private sealed class SqlServerFkRow
    {
        public string? ConstraintName { get; set; }
        public string? ParentTable { get; set; }
        public string? ParentColumn { get; set; }
        public string? ChildTable { get; set; }
        public string? ChildColumn { get; set; }
    }

    private sealed class SqlServerRoutineRow
    {
        public string RoutineName { get; set; } = string.Empty;
        public string RoutineType { get; set; } = "VIEW";
        public string? Definition { get; set; }
    }
}

/// <summary>
/// Extracts schema metadata from MySQL / MariaDB databases using information_schema.
/// </summary>
public sealed class MySqlSchemaExtractor : ISchemaExtractor
{
    public async Task<bool> TestConnectionAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var conn = new MySqlConnection(connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT 1;";
            cmd.CommandTimeout = 5;
            await cmd.ExecuteScalarAsync(cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<DiscoveredSchemaResult> ExtractSchemaAsync(
        Guid workspaceId,
        Guid dataSourceId,
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new MySqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);

        string dbName = conn.Database;

        const string columnsSql = @"
            SELECT 
                c.TABLE_SCHEMA AS SchemaName,
                c.TABLE_NAME AS TableName,
                c.COLUMN_NAME AS ColumnName,
                c.DATA_TYPE AS DataType,
                (c.IS_NULLABLE = 'YES') AS IsNullable,
                c.ORDINAL_POSITION AS OrdinalPosition,
                (c.COLUMN_KEY = 'PRI') AS IsPrimaryKey,
                (c.COLUMN_KEY = 'MUL') AS IsForeignKey
            FROM information_schema.COLUMNS c
            JOIN information_schema.TABLES t 
                ON c.TABLE_SCHEMA = t.TABLE_SCHEMA AND c.TABLE_NAME = t.TABLE_NAME
            WHERE c.TABLE_SCHEMA = @DbName AND t.TABLE_TYPE IN ('BASE TABLE', 'VIEW')
              AND LOWER(c.TABLE_NAME) <> '__efmigrationshistory'
            ORDER BY c.TABLE_NAME, c.ORDINAL_POSITION;";

        var rawColumns = await conn.QueryAsync(new CommandDefinition(columnsSql, new { DbName = dbName }, cancellationToken: cancellationToken));
        var columns = new List<SchemaMetadata>();

        foreach (var col in rawColumns)
        {
            string tableName = col.TableName;
            string colName = col.ColumnName;
            columns.Add(SchemaMetadata.Create(
                workspaceId,
                dataSourceId,
                col.SchemaName,
                tableName,
                colName,
                col.DataType,
                Convert.ToBoolean(col.IsNullable),
                Convert.ToBoolean(col.IsPrimaryKey),
                Convert.ToBoolean(col.IsForeignKey),
                null,
                BusinessNameGenerator.ToFriendlyName(tableName),
                BusinessNameGenerator.ToFriendlyName(colName),
                Convert.ToInt32(col.OrdinalPosition)));
        }

        const string fkSql = @"
            SELECT 
                CONSTRAINT_NAME AS ConstraintName,
                REFERENCED_TABLE_NAME AS ParentTable,
                REFERENCED_COLUMN_NAME AS ParentColumn,
                TABLE_NAME AS ChildTable,
                COLUMN_NAME AS ChildColumn
            FROM information_schema.KEY_COLUMN_USAGE
            WHERE TABLE_SCHEMA = @DbName AND REFERENCED_TABLE_NAME IS NOT NULL
              AND LOWER(TABLE_NAME) <> '__efmigrationshistory'
              AND LOWER(REFERENCED_TABLE_NAME) <> '__efmigrationshistory';";

        var rawFks = await conn.QueryAsync(new CommandDefinition(fkSql, new { DbName = dbName }, cancellationToken: cancellationToken));
        var relationships = new List<TableRelationship>();

        foreach (var fk in rawFks)
        {
            relationships.Add(TableRelationship.Create(
                workspaceId,
                dataSourceId,
                fk.ParentTable,
                fk.ParentColumn,
                fk.ChildTable,
                fk.ChildColumn,
                fk.ConstraintName));
        }

        const string routinesSql = @"
            SELECT TABLE_NAME AS RoutineName, 'VIEW' AS RoutineType, VIEW_DEFINITION AS Definition
            FROM information_schema.VIEWS WHERE TABLE_SCHEMA = @DbName AND LOWER(TABLE_NAME) <> '__efmigrationshistory'
            UNION ALL
            SELECT ROUTINE_NAME AS RoutineName, ROUTINE_TYPE AS RoutineType, ROUTINE_DEFINITION AS Definition
            FROM information_schema.ROUTINES WHERE ROUTINE_SCHEMA = @DbName AND LOWER(ROUTINE_NAME) <> '__efmigrationshistory';";

        var rawRoutines = await conn.QueryAsync(new CommandDefinition(routinesSql, new { DbName = dbName }, cancellationToken: cancellationToken));
        var routines = new List<DatabaseRoutine>();

        foreach (var r in rawRoutines)
        {
            string name = r.RoutineName;
            routines.Add(DatabaseRoutine.Create(
                workspaceId,
                dataSourceId,
                name,
                r.RoutineType,
                r.Definition,
                BusinessNameGenerator.ToFriendlyName(name)));
        }

        return new DiscoveredSchemaResult(columns, relationships, routines);
    }
}
