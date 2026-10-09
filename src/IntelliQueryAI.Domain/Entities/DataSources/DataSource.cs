using IntelliQueryAI.Domain.Enums;
using IntelliQueryAI.Domain.Errors;
using IntelliQueryAI.SharedKernel.Primitives;
using IntelliQueryAI.SharedKernel.Results;

namespace IntelliQueryAI.Domain.Entities.DataSources;

/// <summary>
/// Aggregate root representing an external relational database registered by a workspace.
/// </summary>
public sealed class DataSource : AggregateRoot
{
    /// <summary>
    /// Gets the tenant workspace identifier that owns this data source.
    /// </summary>
    public Guid WorkspaceId { get; private set; }

    /// <summary>
    /// Gets the user-defined display name for the database connection.
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the relational database type (PostgreSQL, SqlServer, MySql).
    /// </summary>
    public DatabaseType DatabaseType { get; private set; }

    /// <summary>
    /// Gets the AES-256 GCM encrypted connection string.
    /// Plaintext connection strings are never stored or logged.
    /// </summary>
    public string EncryptedConnectionString { get; private set; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether this data source is enabled for querying and synchronization.
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Gets the timestamp when the last successful schema sync occurred in UTC.
    /// </summary>
    public DateTime? LastSyncAt { get; private set; }

    /// <summary>
    /// Gets the current status of background synchronization.
    /// </summary>
    public SyncStatus SyncStatus { get; private set; }

    /// <summary>
    /// Gets the error details if the last synchronization failed.
    /// </summary>
    public string? SyncErrorMessage { get; private set; }

    /// <summary>
    /// Gets the creation timestamp in UTC.
    /// </summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>
    /// Gets the last updated timestamp in UTC.
    /// </summary>
    public DateTime? UpdatedAtUtc { get; private set; }

    private DataSource(
        Guid workspaceId,
        string name,
        DatabaseType databaseType,
        string encryptedConnectionString)
        : base(Guid.NewGuid())
    {
        WorkspaceId = workspaceId;
        Name = name;
        DatabaseType = databaseType;
        EncryptedConnectionString = encryptedConnectionString;
        IsActive = true;
        SyncStatus = SyncStatus.Pending;
        CreatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Required for EF Core.
    /// </summary>
    private DataSource()
    {
    }

    /// <summary>
    /// Creates a new DataSource aggregate root.
    /// </summary>
    public static Result<DataSource> Create(
        Guid workspaceId,
        string name,
        DatabaseType databaseType,
        string encryptedConnectionString)
    {
        if (workspaceId == Guid.Empty)
        {
            return Result.Failure<DataSource>(WorkspaceErrors.NotFound);
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<DataSource>(DataSourceErrors.NameRequired);
        }

        if (string.IsNullOrWhiteSpace(encryptedConnectionString))
        {
            return Result.Failure<DataSource>(DataSourceErrors.ConnectionStringRequired);
        }

        var dataSource = new DataSource(workspaceId, name.Trim(), databaseType, encryptedConnectionString);
        return Result.Success(dataSource);
    }

    /// <summary>
    /// Updates data source metadata.
    /// </summary>
    public Result UpdateDetails(string name, DatabaseType databaseType)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure(DataSourceErrors.NameRequired);
        }

        Name = name.Trim();
        DatabaseType = databaseType;
        UpdatedAtUtc = DateTime.UtcNow;
        return Result.Success();
    }

    /// <summary>
    /// Updates the encrypted connection string.
    /// </summary>
    public Result UpdateConnectionString(string encryptedConnectionString)
    {
        if (string.IsNullOrWhiteSpace(encryptedConnectionString))
        {
            return Result.Failure(DataSourceErrors.ConnectionStringRequired);
        }

        EncryptedConnectionString = encryptedConnectionString;
        UpdatedAtUtc = DateTime.UtcNow;
        return Result.Success();
    }

    /// <summary>
    /// Enables the data source for querying and sync.
    /// </summary>
    public void Enable()
    {
        IsActive = true;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Disables the data source.
    /// </summary>
    public void Disable()
    {
        IsActive = false;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks the start of a schema synchronization job.
    /// </summary>
    public void MarkSyncStarted()
    {
        SyncStatus = SyncStatus.InProgress;
        SyncErrorMessage = null;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks the successful completion of a schema synchronization job.
    /// </summary>
    public void MarkSyncCompleted()
    {
        SyncStatus = SyncStatus.Completed;
        SyncErrorMessage = null;
        LastSyncAt = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Records a failed synchronization job.
    /// </summary>
    public void MarkSyncFailed(string errorMessage)
    {
        SyncStatus = SyncStatus.Failed;
        SyncErrorMessage = errorMessage;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
