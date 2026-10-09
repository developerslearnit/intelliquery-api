using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Application.Features.Audit.Dtos;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.SharedKernel.Results;

namespace IntelliQueryAI.Application.Features.Audit.Queries;

/// <summary>
/// Query to retrieve historical audit logs for a workspace.
/// </summary>
public sealed record GetAuditLogsQuery(Guid WorkspaceId, int Limit = 50) : IQuery<Result<IReadOnlyList<AuditLogDto>>>;

public sealed class GetAuditLogsQueryHandler : IQueryHandler<GetAuditLogsQuery, Result<IReadOnlyList<AuditLogDto>>>
{
    private readonly IAuditLogStore _auditLogStore;

    public GetAuditLogsQueryHandler(IAuditLogStore auditLogStore)
    {
        _auditLogStore = auditLogStore;
    }

    public async Task<Result<IReadOnlyList<AuditLogDto>>> HandleAsync(GetAuditLogsQuery query, CancellationToken cancellationToken = default)
    {
        var logs = await _auditLogStore.GetByWorkspaceAsync(query.WorkspaceId, query.Limit, cancellationToken);
        var dtos = logs.Select(l => new AuditLogDto(
            l.Id,
            l.WorkspaceId,
            l.UserId,
            l.Action,
            l.EntityType,
            l.EntityId,
            l.Details,
            l.IpAddress,
            l.TimestampUtc)).ToList();

        return Result.Success<IReadOnlyList<AuditLogDto>>(dtos);
    }
}
