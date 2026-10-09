namespace IntelliQueryAI.Application.Features.Audit.Dtos;

public sealed record AuditLogDto(
    Guid Id,
    Guid? WorkspaceId,
    Guid? UserId,
    string Action,
    string EntityType,
    string? EntityId,
    string Details,
    string? IpAddress,
    DateTime TimestampUtc);
