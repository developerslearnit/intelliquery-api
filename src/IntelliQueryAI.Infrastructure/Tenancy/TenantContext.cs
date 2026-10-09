using IntelliQueryAI.Domain.Services;

namespace IntelliQueryAI.Infrastructure.Tenancy;

/// <summary>
/// Scoped implementation of the tenant context resolved per HTTP request.
/// </summary>
public sealed class TenantContext : ITenantContext
{
    public Guid? WorkspaceId { get; private set; }
    public string? Slug { get; private set; }
    public bool IsResolved => WorkspaceId.HasValue;

    public void SetTenant(Guid workspaceId, string slug)
    {
        WorkspaceId = workspaceId;
        Slug = slug;
    }
}
