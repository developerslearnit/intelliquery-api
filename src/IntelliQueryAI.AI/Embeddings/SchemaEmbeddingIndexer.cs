using System.Diagnostics;
using System.Text;
using System.Text.Json;
using IntelliQueryAI.AI.Telemetry;
using IntelliQueryAI.Domain.Entities.Ai;
using IntelliQueryAI.Domain.Entities.DataSources;
using IntelliQueryAI.Domain.Enums;
using IntelliQueryAI.Domain.Persistence;
using Microsoft.Extensions.Logging;

namespace IntelliQueryAI.AI.Embeddings;

/// <summary>
/// Service abstraction for extracting semantic schema topologies and indexing them into PgVector.
/// </summary>
public interface ISchemaEmbeddingIndexer
{
    Task IndexSchemaAsync(
        Guid workspaceId,
        Guid dataSourceId,
        IReadOnlyList<SchemaMetadata> columns,
        IReadOnlyList<TableRelationship> relationships,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Catalogs discovered database tables, business column aliases, and entity relationships
/// into 1536-dimensional vector embeddings within PostgreSQL PgVector for hybrid retrieval.
/// </summary>
public sealed class SchemaEmbeddingIndexer : ISchemaEmbeddingIndexer
{
    private readonly IVectorStore _vectorStore;
    private readonly IEmbeddingGenerator _embeddingGenerator;
    private readonly ITelemetryService _telemetry;
    private readonly ILogger<SchemaEmbeddingIndexer> _logger;

    public SchemaEmbeddingIndexer(
        IVectorStore vectorStore,
        IEmbeddingGenerator embeddingGenerator,
        ITelemetryService telemetry,
        ILogger<SchemaEmbeddingIndexer> logger)
    {
        _vectorStore = vectorStore;
        _embeddingGenerator = embeddingGenerator;
        _telemetry = telemetry;
        _logger = logger;
    }

    public async Task IndexSchemaAsync(
        Guid workspaceId,
        Guid dataSourceId,
        IReadOnlyList<SchemaMetadata> columns,
        IReadOnlyList<TableRelationship> relationships,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogInformation("Starting schema vector indexing for DataSource {DataSourceId} with {ColumnCount} columns.", dataSourceId, columns.Count);

        try
        {
            // 1. Purge existing vector records for this data source to maintain clean consistency
            try
            {
                await _vectorStore.DeleteByDataSourceIdAsync(workspaceId, dataSourceId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to purge existing vector embeddings for DataSource {DataSourceId}. Continuing.", dataSourceId);
            }

            if (columns.Count == 0)
            {
                _logger.LogInformation("No columns to index for DataSource {DataSourceId}.", dataSourceId);
                return;
            }

            // Group columns by SchemaName and TableName
            var tableGroups = columns
                .GroupBy(c => new { c.SchemaName, c.TableName, c.FriendlyTableName })
                .ToList();

            var embeddingRecords = new List<EmbeddingRecord>();

            foreach (var group in tableGroups)
            {
                var schema = group.Key.SchemaName;
                var table = group.Key.TableName;
                var friendlyTable = group.Key.FriendlyTableName;
                var tableCols = group.OrderBy(c => c.OrdinalPosition).ToList();

                // Foreign key relationships involving this table
                var tableFks = relationships
                    .Where(r => string.Equals(r.ParentTable, table, StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(r.ChildTable, table, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                var sb = new StringBuilder();
                sb.AppendLine($"Table: {schema}.{table}");
                if (!string.IsNullOrWhiteSpace(friendlyTable))
                {
                    sb.AppendLine($"Business Name: {friendlyTable}");
                }

                sb.AppendLine("Columns:");
                foreach (var c in tableCols)
                {
                    string keyInfo = c.IsPrimaryKey ? " [Primary Key]" : c.IsForeignKey ? " [Foreign Key]" : "";
                    sb.AppendLine($"  - {c.ColumnName} ({c.DataType}{keyInfo}): {c.FriendlyColumnName}");
                }

                if (tableFks.Count > 0)
                {
                    sb.AppendLine("Relationships:");
                    foreach (var fk in tableFks)
                    {
                        sb.AppendLine($"  - {fk.ChildTable}.{fk.ChildColumn} -> {fk.ParentTable}.{fk.ParentColumn}");
                    }
                }

                string contentText = sb.ToString();
                var embedding = await _embeddingGenerator.GenerateEmbeddingAsync(contentText, cancellationToken);

                var metadata = JsonSerializer.Serialize(new
                {
                    Schema = schema,
                    Table = table,
                    FriendlyName = friendlyTable,
                    ColumnCount = tableCols.Count
                });

                embeddingRecords.Add(EmbeddingRecord.Create(
                    workspaceId,
                    dataSourceId,
                    EntityType.Table,
                    $"{schema}.{table}",
                    contentText,
                    embedding,
                    metadata));
            }

            // 2. Batch upsert into PgVector store
            await _vectorStore.UpsertBatchAsync(embeddingRecords, cancellationToken);

            sw.Stop();
            _logger.LogInformation(
                "Successfully indexed {RecordCount} table embeddings for DataSource {DataSourceId} in {ElapsedMs}ms.",
                embeddingRecords.Count, dataSourceId, sw.ElapsedMilliseconds);
            _telemetry.TrackToolInvocation("IndexSchemaEmbeddings", sw.ElapsedMilliseconds, true);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Failed to index schema embeddings for DataSource {DataSourceId}.", dataSourceId);
            _telemetry.TrackToolInvocation("IndexSchemaEmbeddings", sw.ElapsedMilliseconds, false);
            // Non-fatal: even if vector store fails, relational metadata in Dapper/EF Core is preserved
        }
    }
}
