using IntelliQueryAI.Domain.Enums;
using IntelliQueryAI.Domain.Services;
using IntelliQueryAI.Infrastructure.SchemaDiscovery.Extractors;

namespace IntelliQueryAI.Infrastructure.SchemaDiscovery;

/// <summary>
/// Orchestrator for schema discovery that delegates to the appropriate database engine extractor.
/// </summary>
public sealed class SchemaDiscoveryEngine : ISchemaDiscoveryEngine
{
    private readonly PostgreSqlSchemaExtractor _postgresExtractor = new();
    private readonly SqlServerSchemaExtractor _sqlServerExtractor = new();
    private readonly MySqlSchemaExtractor _mySqlExtractor = new();

    public Task<bool> TestConnectionAsync(DatabaseType databaseType, string connectionString, CancellationToken cancellationToken = default)
    {
        var extractor = GetExtractor(databaseType);
        return extractor.TestConnectionAsync(connectionString, cancellationToken);
    }

    public async Task<DiscoveredSchemaResult> DiscoverSchemaAsync(
        Guid workspaceId,
        Guid dataSourceId,
        DatabaseType databaseType,
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        var extractor = GetExtractor(databaseType);
        var result = await extractor.ExtractSchemaAsync(workspaceId, dataSourceId, connectionString, cancellationToken);

        // Always exclude EF Core migration history tables (__EFMigrationsHistory) across all database providers
        var filteredColumns = result.Columns
            .Where(c => !string.Equals(c.TableName, "__EFMigrationsHistory", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var filteredRelationships = result.Relationships
            .Where(r => !string.Equals(r.ParentTable, "__EFMigrationsHistory", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(r.ChildTable, "__EFMigrationsHistory", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var filteredRoutines = result.Routines
            .Where(r => !string.Equals(r.RoutineName, "__EFMigrationsHistory", StringComparison.OrdinalIgnoreCase))
            .ToList();

        return new DiscoveredSchemaResult(filteredColumns, filteredRelationships, filteredRoutines);
    }

    private ISchemaExtractor GetExtractor(DatabaseType databaseType) => databaseType switch
    {
        DatabaseType.PostgreSQL => _postgresExtractor,
        DatabaseType.SqlServer => _sqlServerExtractor,
        DatabaseType.MySql => _mySqlExtractor,
        _ => throw new NotSupportedException($"Database engine {databaseType} is not supported.")
    };
}
