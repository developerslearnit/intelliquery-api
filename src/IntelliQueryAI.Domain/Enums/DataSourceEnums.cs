namespace IntelliQueryAI.Domain.Enums;

/// <summary>
/// Supported relational database management systems for external data source connectivity.
/// </summary>
public enum DatabaseType
{
    /// <summary>
    /// PostgreSQL database engine.
    /// </summary>
    PostgreSQL = 0,

    /// <summary>
    /// Microsoft SQL Server database engine.
    /// </summary>
    SqlServer = 1,

    /// <summary>
    /// MySQL / MariaDB database engine.
    /// </summary>
    MySql = 2
}

/// <summary>
/// Status of a data source schema metadata synchronization operation.
/// </summary>
public enum SyncStatus
{
    /// <summary>
    /// Scheduled or awaiting initial synchronization.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Actively connecting and extracting schema metadata.
    /// </summary>
    InProgress = 1,

    /// <summary>
    /// Successfully extracted and updated schema metadata.
    /// </summary>
    Completed = 2,

    /// <summary>
    /// Failed to connect or extract schema metadata.
    /// </summary>
    Failed = 3
}
