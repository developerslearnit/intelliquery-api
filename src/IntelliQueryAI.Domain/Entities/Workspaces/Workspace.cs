using IntelliQueryAI.Domain.Enums;
using IntelliQueryAI.Domain.Errors;
using IntelliQueryAI.Domain.ValueObjects;
using IntelliQueryAI.SharedKernel.Primitives;
using IntelliQueryAI.SharedKernel.Results;

namespace IntelliQueryAI.Domain.Entities.Workspaces;

/// <summary>
/// Aggregate root representing an isolated SaaS tenant workspace.
/// </summary>
public sealed class Workspace : AggregateRoot
{
    private readonly List<WorkspaceMember> _members = [];

    /// <summary>
    /// Gets the friendly business name of the workspace.
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the unique tenant slug used for subdomain routing (e.g., 'acme.aiquery.com').
    /// </summary>
    public TenantSlug Slug { get; private set; } = null!;

    /// <summary>
    /// Gets the current subscription tier of the workspace.
    /// </summary>
    public SubscriptionPlan SubscriptionPlan { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the workspace is active.
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Gets the creation timestamp in UTC.
    /// </summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>
    /// Gets the last updated timestamp in UTC.
    /// </summary>
    public DateTime? UpdatedAtUtc { get; private set; }

    /// <summary>
    /// Gets the members registered to this workspace.
    /// </summary>
    public IReadOnlyCollection<WorkspaceMember> Members => _members.AsReadOnly();

    private Workspace(string name, TenantSlug slug, SubscriptionPlan plan, Guid ownerUserId)
        : base(Guid.NewGuid())
    {
        Name = name;
        Slug = slug;
        SubscriptionPlan = plan;
        IsActive = true;
        CreatedAtUtc = DateTime.UtcNow;

        // Automatically assign the creator as the Owner
        _members.Add(WorkspaceMember.Create(Id, ownerUserId, WorkspaceRole.Owner));
    }

    /// <summary>
    /// Required for EF Core.
    /// </summary>
    private Workspace()
    {
    }

    /// <summary>
    /// Creates a new workspace aggregate root.
    /// </summary>
    /// <param name="name">The workspace name.</param>
    /// <param name="slug">The validated tenant slug.</param>
    /// <param name="plan">The initial subscription plan.</param>
    /// <param name="ownerUserId">The identifier of the user creating the workspace.</param>
    /// <returns>A Result containing the created workspace.</returns>
    public static Result<Workspace> Create(string name, TenantSlug slug, SubscriptionPlan plan, Guid ownerUserId)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<Workspace>(WorkspaceErrors.NameRequired);
        }

        if (slug is null)
        {
            return Result.Failure<Workspace>(WorkspaceErrors.SlugRequired);
        }

        var workspace = new Workspace(name.Trim(), slug, plan, ownerUserId);
        return Result.Success(workspace);
    }

    /// <summary>
    /// Updates the display name of the workspace.
    /// </summary>
    public Result Update(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure(WorkspaceErrors.NameRequired);
        }

        Name = name.Trim();
        UpdatedAtUtc = DateTime.UtcNow;
        return Result.Success();
    }

    /// <summary>
    /// Changes the workspace subscription tier.
    /// </summary>
    public void ChangeSubscriptionPlan(SubscriptionPlan newPlan)
    {
        SubscriptionPlan = newPlan;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Deactivates the workspace.
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Activates the workspace.
    /// </summary>
    public void Activate()
    {
        IsActive = true;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Adds a new user member to the workspace.
    /// </summary>
    public Result<WorkspaceMember> AddMember(Guid userId, WorkspaceRole role)
    {
        if (_members.Any(m => m.UserId == userId))
        {
            return Result.Failure<WorkspaceMember>(WorkspaceErrors.UserAlreadyMember);
        }

        var member = WorkspaceMember.Create(Id, userId, role);
        _members.Add(member);
        UpdatedAtUtc = DateTime.UtcNow;
        return Result.Success(member);
    }

    /// <summary>
    /// Removes a member from the workspace.
    /// </summary>
    public Result RemoveMember(Guid userId)
    {
        var member = _members.FirstOrDefault(m => m.UserId == userId);
        if (member is null)
        {
            return Result.Failure(WorkspaceErrors.MemberNotFound);
        }

        if (member.Role == WorkspaceRole.Owner)
        {
            return Result.Failure(WorkspaceErrors.CannotRemoveOwner);
        }

        _members.Remove(member);
        UpdatedAtUtc = DateTime.UtcNow;
        return Result.Success();
    }
}
