using IntelliQueryAI.SharedKernel.Results;

namespace IntelliQueryAI.Domain.Errors;

/// <summary>
/// Domain errors related to DataSource management, connectivity, and schema discovery.
/// </summary>
public static class DataSourceErrors
{
    public static readonly Error NameRequired =
        Error.Validation("DataSource.NameRequired", "Data source name is required.");

    public static readonly Error ConnectionStringRequired =
        Error.Validation("DataSource.ConnectionStringRequired", "Connection string is required.");

    public static readonly Error NotFound =
        Error.NotFound("DataSource.NotFound", "The specified data source was not found.");

    public static readonly Error Inactive =
        Error.Forbidden("DataSource.Inactive", "This data source is currently disabled.");

    public static Error ConnectionFailed(string details) =>
        Error.Validation("DataSource.ConnectionFailed", $"Failed to establish connection to database: {details}");

    public static readonly Error DecryptionFailed =
        Error.Failure("DataSource.DecryptionFailed", "Failed to decrypt data source connection string.");

    public static readonly Error SyncInProgress =
        Error.Conflict("DataSource.SyncInProgress", "A schema synchronization is already in progress for this data source.");
}
