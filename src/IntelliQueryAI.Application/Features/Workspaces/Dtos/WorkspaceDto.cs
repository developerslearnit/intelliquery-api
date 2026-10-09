namespace IntelliQueryAI.Application.Features.Workspaces.Dtos;

public sealed record WorkspaceDto(
    Guid Id,
    string Name,
    string Slug,
    string SubscriptionPlan,
    bool IsActive,
    DateTime CreatedAtUtc);

public sealed record WorkspaceMemberDto(
    Guid UserId,
    string Email,
    string FullName,
    string Role,
    DateTime JoinedAtUtc);
