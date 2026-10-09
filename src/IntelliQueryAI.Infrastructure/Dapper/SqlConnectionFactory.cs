using System.Data;
using Npgsql;

namespace IntelliQueryAI.Infrastructure.Dapper;

/// <summary>
/// Factory abstraction for creating open relational database connections for Dapper queries.
/// </summary>
public interface ISqlConnectionFactory
{
    /// <summary>
    /// Creates and opens a new database connection asynchronously.
    /// </summary>
    Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Npgsql implementation of connection factory for PostgreSQL.
/// </summary>
public sealed class NpgsqlConnectionFactory : ISqlConnectionFactory
{
    private readonly string _connectionString;

    public NpgsqlConnectionFactory(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public async Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
