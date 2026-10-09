using IntelliQueryAI.SharedKernel.Primitives;

namespace IntelliQueryAI.Domain.Entities.DataSources;

/// <summary>
/// Entity representing discovered column metadata with automated human-friendly aliases.
/// </summary>
public sealed class SchemaMetadata : Entity
{
    public Guid WorkspaceId { get; private set; }
    public Guid DataSourceId { get; private set; }
    public string SchemaName { get; private set; } = "public";
    public string TableName { get; private set; } = string.Empty;
    public string ColumnName { get; private set; } = string.Empty;
    public string DataType { get; private set; } = string.Empty;
    public bool IsNullable { get; private set; }
    public bool IsPrimaryKey { get; private set; }
    public bool IsForeignKey { get; private set; }
    public string? Description { get; private set; }
    public string FriendlyTableName { get; private set; } = string.Empty;
    public string FriendlyColumnName { get; private set; } = string.Empty;
    public int OrdinalPosition { get; private set; }
    public DateTime DiscoveredAtUtc { get; private set; }

    private SchemaMetadata(
        Guid workspaceId,
        Guid dataSourceId,
        string schemaName,
        string tableName,
        string columnName,
        string dataType,
        bool isNullable,
        bool isPrimaryKey,
        bool isForeignKey,
        string? description,
        string friendlyTableName,
        string friendlyColumnName,
        int ordinalPosition)
        : base(Guid.NewGuid())
    {
        WorkspaceId = workspaceId;
        DataSourceId = dataSourceId;
        SchemaName = schemaName;
        TableName = tableName;
        ColumnName = columnName;
        DataType = dataType;
        IsNullable = isNullable;
        IsPrimaryKey = isPrimaryKey;
        IsForeignKey = isForeignKey;
        Description = description;
        FriendlyTableName = friendlyTableName;
        FriendlyColumnName = friendlyColumnName;
        OrdinalPosition = ordinalPosition;
        DiscoveredAtUtc = DateTime.UtcNow;
    }

    private SchemaMetadata() { }

    public static SchemaMetadata Create(
        Guid workspaceId,
        Guid dataSourceId,
        string schemaName,
        string tableName,
        string columnName,
        string dataType,
        bool isNullable,
        bool isPrimaryKey,
        bool isForeignKey,
        string? description,
        string friendlyTableName,
        string friendlyColumnName,
        int ordinalPosition)
    {
        return new SchemaMetadata(
            workspaceId,
            dataSourceId,
            schemaName,
            tableName,
            columnName,
            dataType,
            isNullable,
            isPrimaryKey,
            isForeignKey,
            description,
            friendlyTableName,
            friendlyColumnName,
            ordinalPosition);
    }

    public void UpdateDescription(string description)
    {
        Description = description;
    }

    public void UpdateFriendlyNames(string friendlyTable, string friendlyColumn)
    {
        FriendlyTableName = friendlyTable;
        FriendlyColumnName = friendlyColumn;
    }
}

/// <summary>
/// Entity representing foreign key parent-child relationships between tables.
/// </summary>
public sealed class TableRelationship : Entity
{
    public Guid WorkspaceId { get; private set; }
    public Guid DataSourceId { get; private set; }
    public string ParentTable { get; private set; } = string.Empty;
    public string ParentColumn { get; private set; } = string.Empty;
    public string ChildTable { get; private set; } = string.Empty;
    public string ChildColumn { get; private set; } = string.Empty;
    public string? ConstraintName { get; private set; }
    public DateTime DiscoveredAtUtc { get; private set; }

    private TableRelationship(
        Guid workspaceId,
        Guid dataSourceId,
        string parentTable,
        string parentColumn,
        string childTable,
        string childColumn,
        string? constraintName)
        : base(Guid.NewGuid())
    {
        WorkspaceId = workspaceId;
        DataSourceId = dataSourceId;
        ParentTable = parentTable;
        ParentColumn = parentColumn;
        ChildTable = childTable;
        ChildColumn = childColumn;
        ConstraintName = constraintName;
        DiscoveredAtUtc = DateTime.UtcNow;
    }

    private TableRelationship() { }

    public static TableRelationship Create(
        Guid workspaceId,
        Guid dataSourceId,
        string parentTable,
        string parentColumn,
        string childTable,
        string childColumn,
        string? constraintName = null)
    {
        return new TableRelationship(
            workspaceId,
            dataSourceId,
            parentTable,
            parentColumn,
            childTable,
            childColumn,
            constraintName);
    }
}

/// <summary>
/// Entity representing discovered database views or stored procedures.
/// </summary>
public sealed class DatabaseRoutine : Entity
{
    public Guid WorkspaceId { get; private set; }
    public Guid DataSourceId { get; private set; }
    public string RoutineName { get; private set; } = string.Empty;
    public string RoutineType { get; private set; } = string.Empty; // "VIEW" or "PROCEDURE"
    public string? Definition { get; private set; }
    public string FriendlyName { get; private set; } = string.Empty;
    public DateTime DiscoveredAtUtc { get; private set; }

    private DatabaseRoutine(
        Guid workspaceId,
        Guid dataSourceId,
        string routineName,
        string routineType,
        string? definition,
        string friendlyName)
        : base(Guid.NewGuid())
    {
        WorkspaceId = workspaceId;
        DataSourceId = dataSourceId;
        RoutineName = routineName;
        RoutineType = routineType;
        Definition = definition;
        FriendlyName = friendlyName;
        DiscoveredAtUtc = DateTime.UtcNow;
    }

    private DatabaseRoutine() { }

    public static DatabaseRoutine Create(
        Guid workspaceId,
        Guid dataSourceId,
        string routineName,
        string routineType,
        string? definition,
        string friendlyName)
    {
        return new DatabaseRoutine(workspaceId, dataSourceId, routineName, routineType, definition, friendlyName);
    }
}
