using System.Diagnostics;
using System.Text;
using IntelliQueryAI.AI.Telemetry;
using IntelliQueryAI.Domain.Enums;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.Domain.Services;
using Pgvector;

namespace IntelliQueryAI.AI.Tools;

public sealed record FormattedSchemaContext(
    string FormattedText,
    IReadOnlyList<string> RelevantTables,
    IReadOnlyList<string> RelevantColumns);

public sealed class SchemaRetrievalTools
{
    private readonly IVectorStore _vectorStore;
    private readonly ISchemaReadStore _schemaReadStore;
    private readonly ITelemetryService _telemetry;

    public SchemaRetrievalTools(
        IVectorStore vectorStore,
        ISchemaReadStore schemaReadStore,
        ITelemetryService telemetry)
    {
        _vectorStore = vectorStore;
        _schemaReadStore = schemaReadStore;
        _telemetry = telemetry;
    }

    public Task<FormattedSchemaContext> RetrieveRelevantSchemaAsync(
        Guid workspaceId,
        Guid dataSourceId,
        Vector queryEmbedding,
        int topK = 15,
        CancellationToken cancellationToken = default)
    {
        return RetrieveRelevantSchemaAsync(workspaceId, dataSourceId, string.Empty, queryEmbedding, topK, cancellationToken);
    }

    public async Task<FormattedSchemaContext> RetrieveRelevantSchemaAsync(
        Guid workspaceId,
        Guid dataSourceId,
        string userQuestion,
        Vector queryEmbedding,
        int topK = 15,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        // 1. Retrieve all tables cataloged for this data source
        var allTables = (await _schemaReadStore.GetTablesAsync(workspaceId, dataSourceId, cancellationToken)) ?? Array.Empty<TableSummaryDto>();

        IReadOnlyList<VectorSearchResult> searchResults = Array.Empty<VectorSearchResult>();
        try
        {
            searchResults = (await _vectorStore.SearchSimilarAsync(
                workspaceId,
                dataSourceId,
                queryEmbedding,
                limit: 30,
                minSimilarity: 0.20,
                cancellationToken)) ?? Array.Empty<VectorSearchResult>();
        }
        catch
        {
            // Vector store fallback
        }

        // If no tables in Dapper read store, but vector search found tables (e.g. mock in unit tests or pre-indexed embeddings)
        if (allTables.Count == 0 && searchResults.Count > 0)
        {
            allTables = searchResults
                .Select(r => r.EntityType == "Table" ? r.EntityIdentifier : r.EntityIdentifier.Split('.')[0])
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(t => new TableSummaryDto(dataSourceId, "Data Source", "dbo", t, t, 0))
                .ToList();
        }

        if (allTables.Count == 0)
        {
            sw.Stop();
            _telemetry.TrackToolInvocation("RetrieveRelevantSchema", sw.ElapsedMilliseconds, false);
            return new FormattedSchemaContext(string.Empty, Array.Empty<string>(), Array.Empty<string>());
        }

        // 2. Fetch full columns and relationships in batch for fast in-memory indexing
        var allColumns = (await _schemaReadStore.GetColumnsAsync(workspaceId, dataSourceId, null, cancellationToken)) ?? Array.Empty<ColumnMetadataDto>();
        if (allColumns.Count == 0 && allTables.Count > 0)
        {
            var list = new List<ColumnMetadataDto>();
            foreach (var t in allTables)
            {
                var cols = await _schemaReadStore.GetColumnsAsync(workspaceId, dataSourceId, t.TableName, cancellationToken);
                if (cols != null) list.AddRange(cols);
            }
            allColumns = list;
        }

        var relationships = (await _schemaReadStore.GetRelationshipsAsync(workspaceId, dataSourceId, cancellationToken)) ?? Array.Empty<RelationshipDto>();

        var columnsByTable = allColumns
            .GroupBy(c => c.TableName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<ColumnMetadataDto>)g.ToList(), StringComparer.OrdinalIgnoreCase);

        // 3. Extract keywords from user question for lexical / semantic hybrid ranking
        var questionTokens = ExtractMeaningfulTokens(userQuestion);

        // 4. Hybrid Selection Logic
        List<string> selectedTableNames;

        if (allTables.Count <= 20)
        {
            // For standard schemas (<= 20 tables), include all tables to guarantee 0 missing entity errors,
            // ordered by keyword relevance so top matching tables appear first.
            selectedTableNames = allTables
                .OrderByDescending(t => ScoreTableLexically(t, questionTokens, columnsByTable))
                .Select(t => t.TableName)
                .ToList();
        }
        else
        {
            // For enterprise schemas (> 20 tables), execute multi-signal Hybrid Retrieval (PgVector + BM25 Lexical + FK Graph)
            var vectorScores = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in searchResults)
            {
                string tbl = r.EntityIdentifier.Contains('.')
                    ? r.EntityIdentifier.Split('.')[1]
                    : r.EntityIdentifier;

                if (!vectorScores.ContainsKey(tbl) || vectorScores[tbl] < r.Similarity)
                {
                    vectorScores[tbl] = r.Similarity;
                }
            }

            var scoredTables = allTables.Select(t =>
            {
                double vecScore = vectorScores.TryGetValue(t.TableName, out var s) ? s : 0.0;
                double lexScore = ScoreTableLexically(t, questionTokens, columnsByTable);
                double totalScore = (vecScore * 100.0) + lexScore;
                return (Table: t.TableName, Score: totalScore);
            })
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .Select(x => x.Table)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

            // Expand foreign key relationships for selected tables so JOINs work seamlessly
            var fkExpanded = new HashSet<string>(scoredTables, StringComparer.OrdinalIgnoreCase);
            foreach (var rel in relationships)
            {
                if (fkExpanded.Contains(rel.ParentTable) && !fkExpanded.Contains(rel.ChildTable))
                {
                    fkExpanded.Add(rel.ChildTable);
                }
                else if (fkExpanded.Contains(rel.ChildTable) && !fkExpanded.Contains(rel.ParentTable))
                {
                    fkExpanded.Add(rel.ParentTable);
                }

                if (fkExpanded.Count >= 20) break;
            }

            selectedTableNames = fkExpanded.ToList();
        }

        // 5. Build rich, formatted schema context
        var sb = new StringBuilder();
        var relevantCols = new List<string>();

        foreach (var tableName in selectedTableNames)
        {
            if (!columnsByTable.TryGetValue(tableName, out var cols) || cols.Count == 0)
            {
                continue;
            }

            var sampleCol = cols[0];
            string schemaPrefix = !string.IsNullOrWhiteSpace(sampleCol.SchemaName) ? $"{sampleCol.SchemaName}." : "";
            string friendlyPart = !string.IsNullOrWhiteSpace(sampleCol.FriendlyTableName)
                ? $" (Business: {sampleCol.FriendlyTableName})"
                : "";

            sb.AppendLine($"Table: {schemaPrefix}{tableName}{friendlyPart}");
            sb.Append("  Columns: ");

            var colDefs = cols.Select(c =>
            {
                string key = c.IsPrimaryKey ? " [PK]" : c.IsForeignKey ? " [FK]" : "";
                relevantCols.Add($"{tableName}.{c.ColumnName}");
                return $"{c.ColumnName} ({c.DataType}{key}, Business: {c.FriendlyColumnName})";
            });

            sb.AppendLine(string.Join(", ", colDefs));
        }

        // 6. Append active Foreign Key Relationships
        var tableRel = relationships
            .Where(r => selectedTableNames.Contains(r.ParentTable, StringComparer.OrdinalIgnoreCase) ||
                        selectedTableNames.Contains(r.ChildTable, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (tableRel.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Foreign Key Relationships:");
            foreach (var rel in tableRel)
            {
                sb.AppendLine($"  {rel.ChildTable}.{rel.ChildColumn} -> {rel.ParentTable}.{rel.ParentColumn}");
            }
        }

        sw.Stop();
        _telemetry.TrackToolInvocation("RetrieveRelevantSchema", sw.ElapsedMilliseconds, true);

        return new FormattedSchemaContext(sb.ToString(), selectedTableNames, relevantCols);
    }

    private static double ScoreTableLexically(
        TableSummaryDto table,
        IReadOnlyList<string> tokens,
        IReadOnlyDictionary<string, IReadOnlyList<ColumnMetadataDto>> columnsByTable)
    {
        if (tokens.Count == 0) return 0.0;
        double score = 0.0;

        foreach (var token in tokens)
        {
            // Exact table name match
            if (string.Equals(table.TableName, token, StringComparison.OrdinalIgnoreCase))
            {
                score += 100.0;
            }
            else if (table.TableName.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                score += 50.0;
            }

            // Friendly table name match (e.g. "Ebs Transactions" matches "ebs" and "transactions")
            if (table.FriendlyTableName.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                score += 45.0;
            }

            // Schema match (e.g. schema "ebs")
            if (table.SchemaName.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                score += 35.0;
            }

            // Column names match
            if (columnsByTable.TryGetValue(table.TableName, out var cols))
            {
                foreach (var c in cols)
                {
                    if (c.ColumnName.Contains(token, StringComparison.OrdinalIgnoreCase))
                    {
                        score += 15.0;
                    }
                    else if (c.FriendlyColumnName.Contains(token, StringComparison.OrdinalIgnoreCase))
                    {
                        score += 10.0;
                    }
                }
            }
        }

        return score;
    }

    private static IReadOnlyList<string> ExtractMeaningfulTokens(string? question)
    {
        if (string.IsNullOrWhiteSpace(question)) return Array.Empty<string>();

        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "show", "me", "the", "top", "find", "all", "get", "list", "of", "and", "in",
            "by", "for", "with", "from", "what", "how", "many", "is", "are", "a", "an",
            "on", "at", "to", "or", "as", "please", "can", "you", "give"
        };

        var words = question
            .Split(new[] { ' ', ',', '.', '?', '!', ';', ':', '(', ')', '[', ']', '_', '-' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 2 && !stopWords.Contains(w) && !long.TryParse(w, out _))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Also add singular forms for common plural words (e.g., "transactions" -> "transaction")
        var derived = new List<string>(words);
        foreach (var w in words)
        {
            if (w.EndsWith("ies", StringComparison.OrdinalIgnoreCase) && w.Length > 3)
            {
                derived.Add(w[..^3] + "y");
            }
            else if (w.EndsWith("es", StringComparison.OrdinalIgnoreCase) && w.Length > 2)
            {
                derived.Add(w[..^2]);
            }
            else if (w.EndsWith("s", StringComparison.OrdinalIgnoreCase) && w.Length > 2)
            {
                derived.Add(w[..^1]);
            }
        }

        return derived.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
}

public sealed class SqlExecutionTools
{
    private readonly ISafeQueryExecutor _queryExecutor;
    private readonly ITelemetryService _telemetry;

    public SqlExecutionTools(
        ISafeQueryExecutor queryExecutor,
        ITelemetryService telemetry)
    {
        _queryExecutor = queryExecutor;
        _telemetry = telemetry;
    }

    public async Task<ExecutionQueryResult> ExecuteSafeQueryAsync(
        DatabaseType dbType,
        string connectionString,
        string sanitizedSql,
        int timeoutSeconds = 15,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var result = await _queryExecutor.ExecuteSafeQueryAsync(
            dbType,
            connectionString,
            sanitizedSql,
            timeoutSeconds,
            cancellationToken);

        sw.Stop();
        _telemetry.TrackToolInvocation("ExecuteSafeQuery", sw.ElapsedMilliseconds, result.IsSuccess);
        return result;
    }
}
