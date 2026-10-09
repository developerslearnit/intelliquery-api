namespace IntelliQueryAI.Application.Features.Governance.Dtos;

public sealed record GovernancePolicyDto(
    Guid Id,
    Guid WorkspaceId,
    string Name,
    string? Description,
    string TargetTable,
    string TargetColumn,
    string Classification,
    string MaskingStrategy,
    string? MaskingPattern,
    IReadOnlyList<string> AllowedRoles,
    bool IsActive,
    bool IsAiMaskingEnforced,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed record CreateGovernancePolicyRequestDto(
    string Name,
    string? Description = null,
    string TargetTable = "*",
    string TargetColumn = "*",
    string Classification = "Pii",
    string MaskingStrategy = "Partial",
    string? MaskingPattern = null,
    IReadOnlyList<string>? AllowedRoles = null,
    bool IsActive = true,
    bool IsAiMaskingEnforced = true);

public sealed record UpdateGovernancePolicyRequestDto(
    string Name,
    string? Description = null,
    string TargetTable = "*",
    string TargetColumn = "*",
    string Classification = "Pii",
    string MaskingStrategy = "Partial",
    string? MaskingPattern = null,
    IReadOnlyList<string>? AllowedRoles = null,
    bool IsActive = true,
    bool IsAiMaskingEnforced = true);

public sealed record TestMaskingPreviewRequestDto(
    string SampleValue,
    string MaskingStrategy = "Partial",
    string? MaskingPattern = null);

public sealed record TestMaskingPreviewResponseDto(
    string OriginalValue,
    string MaskedValue,
    string MaskingStrategy);

public sealed record GovernanceAuditLogDto(
    Guid Id,
    Guid? UserId,
    string Action,
    string EntityType,
    string? EntityId,
    string Details,
    DateTime TimestampUtc);

public sealed record ToggleGovernancePolicyRequestDto(
    bool? IsActive = null);
