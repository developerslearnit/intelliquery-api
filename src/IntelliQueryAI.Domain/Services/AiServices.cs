using IntelliQueryAI.Domain.Enums;

namespace IntelliQueryAI.Domain.Services;

public sealed record ExecutionQueryResult(
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows,
    int RowCount,
    long ExecutionDurationMs,
    bool IsSuccess,
    string? ErrorMessage);

public interface ISafeQueryExecutor
{
    Task<ExecutionQueryResult> ExecuteSafeQueryAsync(
        DatabaseType dbType,
        string connectionString,
        string sanitizedSql,
        int timeoutSeconds = 15,
        CancellationToken cancellationToken = default);
}
