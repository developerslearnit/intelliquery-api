using System.Data;
using Dapper;
using IntelliQueryAI.Domain.Entities.Ai;
using IntelliQueryAI.Domain.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Pgvector;

namespace IntelliQueryAI.Infrastructure.Persistence.Stores;

public sealed class PgVectorEmbeddingStore : IVectorStore
{
    private readonly IntelliQueryDbContext _dbContext;
    private readonly string _connectionString;

    public PgVectorEmbeddingStore(
        IntelliQueryDbContext dbContext,
        Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        _dbContext = dbContext;
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=intelliquery;Username=postgres;Password=postgres;";
    }

    public async Task UpsertAsync(EmbeddingRecord record, CancellationToken cancellationToken = default)
    {
        await _dbContext.EmbeddingRecords.AddAsync(record, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpsertBatchAsync(IReadOnlyList<EmbeddingRecord> records, CancellationToken cancellationToken = default)
    {
        if (records.Count == 0) return;
        await _dbContext.EmbeddingRecords.AddRangeAsync(records, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<VectorSearchResult>> SearchSimilarAsync(
        Guid workspaceId,
        Guid dataSourceId,
        Vector queryEmbedding,
        int limit = 15,
        double minSimilarity = 0.35,
        CancellationToken cancellationToken = default)
    {
        try
        {
            const string sql = """
                SELECT 
                    "Id",
                    "WorkspaceId",
                    "DataSourceId",
                    "EntityType",
                    "EntityIdentifier",
                    "ContentText",
                    "MetadataJson",
                    (1 - ("Embedding" <=> CAST(@QueryVector AS vector))) AS "Similarity"
                FROM embedding_records
                WHERE "WorkspaceId" = @WorkspaceId 
                  AND "DataSourceId" = @DataSourceId
                ORDER BY "Embedding" <=> CAST(@QueryVector AS vector)
                LIMIT @Limit;
                """;

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var parameters = new
            {
                WorkspaceId = workspaceId,
                DataSourceId = dataSourceId,
                // Dapper has no type handler for Pgvector.Vector; send the pgvector text literal ('[x,y,...]') and cast in SQL.
                QueryVector = "[" + string.Join(",", queryEmbedding.ToArray().Select(v => v.ToString(System.Globalization.CultureInfo.InvariantCulture))) + "]",
                Limit = limit
            };

            var results = await connection.QueryAsync<VectorSearchResult>(
                new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));

            return results
                .Where(r => r.Similarity >= minSimilarity)
                .ToList();
        }
        catch
        {
            return Array.Empty<VectorSearchResult>();
        }
    }

    public async Task DeleteByDataSourceIdAsync(Guid workspaceId, Guid dataSourceId, CancellationToken cancellationToken = default)
    {
        await _dbContext.EmbeddingRecords
            .Where(e => e.WorkspaceId == workspaceId && e.DataSourceId == dataSourceId)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
