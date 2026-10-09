using IntelliQueryAI.Domain.Enums;

namespace IntelliQueryAI.Application.Features.DataSources.Dtos;

public sealed record DataSourceDto(
    Guid Id,
    Guid WorkspaceId,
    string Name,
    string DatabaseType,
    bool IsActive,
    DateTime? LastSyncAt,
    string SyncStatus,
    string? SyncErrorMessage,
    DateTime CreatedAtUtc)
{
    public DateTime? LastSyncedAt => LastSyncAt;
    public string DatabaseTypeName => DatabaseType;
    public DateTime CreatedAt => CreatedAtUtc;
}

public sealed record TestConnectionResultDto(
    bool IsSuccessful,
    string Message,
    long LatencyMs)
{
    public bool Success => IsSuccessful;
}

public sealed record SyncResultDto(
    Guid DataSourceId,
    int TablesCount,
    int ColumnsCount,
    int RelationshipsCount,
    int RoutinesCount,
    long DurationMs,
    bool Success,
    string Message = "");

