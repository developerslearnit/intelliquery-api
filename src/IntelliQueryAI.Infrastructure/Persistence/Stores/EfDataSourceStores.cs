using IntelliQueryAI.Domain.Entities.DataSources;
using IntelliQueryAI.Domain.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IntelliQueryAI.Infrastructure.Persistence.Stores;

public sealed class EfDataSourceStore : IDataSourceStore
{
    private readonly IntelliQueryDbContext _context;

    public EfDataSourceStore(IntelliQueryDbContext context)
    {
        _context = context;
    }

    public async Task<DataSource?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.DataSources.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<DataSource>> GetByWorkspaceIdAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        return await _context.DataSources
            .Where(d => d.WorkspaceId == workspaceId)
            .OrderBy(d => d.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountByWorkspaceIdAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        return await _context.DataSources
            .CountAsync(d => d.WorkspaceId == workspaceId && d.IsActive, cancellationToken);
    }

    public async Task AddAsync(DataSource dataSource, CancellationToken cancellationToken = default)
    {
        await _context.DataSources.AddAsync(dataSource, cancellationToken);
    }

    public Task DeleteAsync(DataSource dataSource, CancellationToken cancellationToken = default)
    {
        _context.DataSources.Remove(dataSource);
        return Task.CompletedTask;
    }
}

public sealed class EfSchemaMetadataStore : ISchemaMetadataStore
{
    private readonly IntelliQueryDbContext _context;

    public EfSchemaMetadataStore(IntelliQueryDbContext context)
    {
        _context = context;
    }

    public async Task ReplaceSchemaAsync(
        Guid workspaceId,
        Guid dataSourceId,
        IReadOnlyList<SchemaMetadata> columns,
        IReadOnlyList<TableRelationship> relationships,
        IReadOnlyList<DatabaseRoutine> routines,
        CancellationToken cancellationToken = default)
    {
        // Remove existing snapshot for this data source
        var existingColumns = await _context.SchemaMetadata
            .Where(s => s.WorkspaceId == workspaceId && s.DataSourceId == dataSourceId)
            .ToListAsync(cancellationToken);
        _context.SchemaMetadata.RemoveRange(existingColumns);

        var existingRel = await _context.TableRelationships
            .Where(r => r.WorkspaceId == workspaceId && r.DataSourceId == dataSourceId)
            .ToListAsync(cancellationToken);
        _context.TableRelationships.RemoveRange(existingRel);

        var existingRoutines = await _context.DatabaseRoutines
            .Where(r => r.WorkspaceId == workspaceId && r.DataSourceId == dataSourceId)
            .ToListAsync(cancellationToken);
        _context.DatabaseRoutines.RemoveRange(existingRoutines);

        // Add fresh extracted snapshot, strictly excluding __EFMigrationsHistory
        var cleanColumns = columns
            .Where(c => !string.Equals(c.TableName, "__EFMigrationsHistory", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var cleanRel = relationships
            .Where(r => !string.Equals(r.ParentTable, "__EFMigrationsHistory", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(r.ChildTable, "__EFMigrationsHistory", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var cleanRoutines = routines
            .Where(r => !string.Equals(r.RoutineName, "__EFMigrationsHistory", StringComparison.OrdinalIgnoreCase))
            .ToList();

        await _context.SchemaMetadata.AddRangeAsync(cleanColumns, cancellationToken);
        await _context.TableRelationships.AddRangeAsync(cleanRel, cancellationToken);
        await _context.DatabaseRoutines.AddRangeAsync(cleanRoutines, cancellationToken);
    }
}
