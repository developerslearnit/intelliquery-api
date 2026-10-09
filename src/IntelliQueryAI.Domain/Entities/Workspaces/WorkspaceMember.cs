using IntelliQueryAI.Domain.Enums;
using IntelliQueryAI.SharedKernel.Primitives;

namespace IntelliQueryAI.Domain.Entities.Workspaces;

/// <summary>
/// Represents a user membership and their role within a specific workspace tenant.
/// </summary>
public sealed class WorkspaceMember : Entity
{
    /// <summary>
    /// Gets the associated workspace identifier.
    /// </summary>
    public Guid WorkspaceId { get; private set; }

    /// <summary>
    /// Gets the associated user identifier.
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>
    /// Gets the role granted to the user in this workspace.
    /// </summary>
    public WorkspaceRole Role { get; private set; }

    /// <summary>
    /// Gets the timestamp when the membership was created in UTC.
    /// </summary>
    public DateTime JoinedAtUtc { get; private set; }

    private WorkspaceMember(Guid workspaceId, Guid userId, WorkspaceRole role)
        : base(Guid.NewGuid())
    {
        WorkspaceId = workspaceId;
        UserId = userId;
        Role = role;
        JoinedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Required for EF Core.
    /// </summary>
    private WorkspaceMember()
    {
    }

    /// <summary>
    /// Factory method to instantiate a new workspace membership.
    /// </summary>
    public static WorkspaceMember Create(Guid workspaceId, Guid userId, WorkspaceRole role)
    {
        return new WorkspaceMember(workspaceId, userId, role);
    }

    /// <summary>
    /// Updates the member's role within the workspace.
    /// </summary>
    /// <param name="newRole">The new workspace role.</param>
    public void UpdateRole(WorkspaceRole newRole)
    {
        Role = newRole;
    }
}
