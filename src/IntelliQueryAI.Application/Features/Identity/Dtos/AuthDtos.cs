namespace IntelliQueryAI.Application.Features.Identity.Dtos;

public sealed record UserDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string SystemRole,
    bool IsEmailVerified);

public sealed record AuthResponseDto(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAtUtc,
    UserDto User,
    Workspaces.Dtos.WorkspaceDto? ActiveWorkspace);
