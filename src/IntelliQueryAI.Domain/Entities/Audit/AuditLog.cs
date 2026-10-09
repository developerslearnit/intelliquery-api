using IntelliQueryAI.SharedKernel.Primitives;

namespace IntelliQueryAI.Domain.Entities.Audit;

/// <summary>
/// Entity recording tenant audit log events for compliance and security auditing.
/// </summary>
public sealed class AuditLog : Entity
{
    /// <summary>
    /// Gets the tenant workspace identifier associated with this action, if any.
    /// </summary>
    public Guid? WorkspaceId { get; private set; }

    /// <summary>
    /// Gets the identifier of the user who performed the action, if authenticated.
    /// </summary>
    public Guid? UserId { get; private set; }

    /// <summary>
    /// Gets the action name (e.g. 'User.Register', 'Workspace.Create', 'Member.Add').
    /// </summary>
    public string Action { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the type of entity impacted (e.g. 'Workspace', 'User').
    /// </summary>
    public string EntityType { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the identifier of the entity impacted.
    /// </summary>
    public string? EntityId { get; private set; }

    /// <summary>
    /// Gets contextual JSON or human-readable details of the operation.
    /// </summary>
    public string Details { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the client IP address.
    /// </summary>
    public string? IpAddress { get; private set; }

    /// <summary>
    /// Gets the client User-Agent string.
    /// </summary>
    public string? UserAgent { get; private set; }

    /// <summary>
    /// Gets the timestamp when the audit entry occurred in UTC.
    /// </summary>
    public DateTime TimestampUtc { get; private set; }

    private AuditLog(
        Guid? workspaceId,
        Guid? userId,
        string action,
        string entityType,
        string? entityId,
        string details,
        string? ipAddress,
        string? userAgent)
        : base(Guid.NewGuid())
    {
        WorkspaceId = workspaceId;
        UserId = userId;
        Action = action;
        EntityType = entityType;
        EntityId = entityId;
        Details = details;
        IpAddress = ipAddress;
        UserAgent = userAgent;
        TimestampUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Required for EF Core.
    /// </summary>
    private AuditLog()
    {
    }

    /// <summary>
    /// Factory method to create an audit log record.
    /// </summary>
    public static AuditLog Create(
        Guid? workspaceId,
        Guid? userId,
        string action,
        string entityType,
        string? entityId,
        string details,
        string? ipAddress = null,
        string? userAgent = null)
    {
        return new AuditLog(workspaceId, userId, action, entityType, entityId, details, ipAddress, userAgent);
    }
}
