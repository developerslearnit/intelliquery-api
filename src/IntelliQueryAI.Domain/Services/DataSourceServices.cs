using IntelliQueryAI.Domain.Entities.DataSources;
using IntelliQueryAI.Domain.Enums;

namespace IntelliQueryAI.Domain.Services;

/// <summary>
/// Service abstraction for encrypting and decrypting sensitive data source credentials.
/// Implements authenticated AES-256 GCM encryption.
/// </summary>
public interface IEncryptionService
{
    /// <summary>
    /// Encrypts plaintext string using AES-256 GCM and returns a base64 encoded payload with nonce and authentication tag.
    /// </summary>
    string Encrypt(string plainText);

    /// <summary>
    /// Decrypts authenticated ciphertext payload back into plaintext.
    /// </summary>
    string Decrypt(string cipherText);
}

/// <summary>
/// Represents the structured output of an automated schema discovery execution.
/// </summary>
public sealed record DiscoveredSchemaResult(
    IReadOnlyList<SchemaMetadata> Columns,
    IReadOnlyList<TableRelationship> Relationships,
    IReadOnlyList<DatabaseRoutine> Routines);

/// <summary>
/// Engine capable of connecting to supported database management systems and extracting their schema topology.
/// </summary>
public interface ISchemaDiscoveryEngine
{
    /// <summary>
    /// Validates network connectivity and authentication against the target database.
    /// </summary>
    Task<bool> TestConnectionAsync(DatabaseType databaseType, string connectionString, CancellationToken cancellationToken = default);

    /// <summary>
    /// Connects to the database and extracts tables, columns, indexes, foreign keys, views, and stored procedures.
    /// </summary>
    Task<DiscoveredSchemaResult> DiscoverSchemaAsync(
        Guid workspaceId,
        Guid dataSourceId,
        DatabaseType databaseType,
        string connectionString,
        CancellationToken cancellationToken = default);
}
