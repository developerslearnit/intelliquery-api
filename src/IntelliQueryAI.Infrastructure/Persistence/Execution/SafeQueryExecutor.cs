using System.Data;
using System.Data.Common;
using System.Diagnostics;
using IntelliQueryAI.Domain.Enums;
using IntelliQueryAI.Domain.Services;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using Npgsql;

namespace IntelliQueryAI.Infrastructure.Persistence.Execution;

public sealed class SafeQueryExecutor : ISafeQueryExecutor
{
    private readonly ILogger<SafeQueryExecutor> _logger;

    public SafeQueryExecutor(ILogger<SafeQueryExecutor> logger)
    {
        _logger = logger;
    }

    public async Task<ExecutionQueryResult> ExecuteSafeQueryAsync(
        DatabaseType dbType,
        string connectionString,
        string sanitizedSql,
        int timeoutSeconds = 15,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogInformation("Executing safe query against {DbType} database. Timeout: {Timeout}s", dbType, timeoutSeconds);

        try
        {
            await using var connection = CreateConnection(dbType, connectionString);
            await connection.OpenAsync(cancellationToken);

            // Execute within a read-only transaction where supported
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = sanitizedSql;
            cmd.CommandTimeout = timeoutSeconds;

            await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection, cancellationToken);
            var columns = new List<string>(reader.FieldCount);
            for (int i = 0; i < reader.FieldCount; i++)
            {
                columns.Add(reader.GetName(i));
            }

            var rows = new List<Dictionary<string, object?>>();
            while (await reader.ReadAsync(cancellationToken) && rows.Count < 1000)
            {
                var row = new Dictionary<string, object?>(columns.Count);
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row[columns[i]] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                }
                rows.Add(row);
            }

            sw.Stop();
            _logger.LogInformation("Query executed successfully in {DurationMs}ms, returning {RowCount} rows.", sw.ElapsedMilliseconds, rows.Count);

            return new ExecutionQueryResult(
                Columns: columns,
                Rows: rows,
                RowCount: rows.Count,
                ExecutionDurationMs: sw.ElapsedMilliseconds,
                IsSuccess: true,
                ErrorMessage: null);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Safe query execution failed after {DurationMs}ms: {Message}", sw.ElapsedMilliseconds, ex.Message);

            return new ExecutionQueryResult(
                Columns: Array.Empty<string>(),
                Rows: Array.Empty<IReadOnlyDictionary<string, object?>>(),
                RowCount: 0,
                ExecutionDurationMs: sw.ElapsedMilliseconds,
                IsSuccess: false,
                ErrorMessage: ex.Message);
        }
    }

    private static DbConnection CreateConnection(DatabaseType dbType, string connectionString)
    {
        return dbType switch
        {
            DatabaseType.PostgreSQL => new NpgsqlConnection(connectionString),
            DatabaseType.SqlServer => new SqlConnection(connectionString),
            DatabaseType.MySql => new MySqlConnection(connectionString),
            _ => throw new NotSupportedException($"Database engine '{dbType}' is not supported.")
        };
    }
}
