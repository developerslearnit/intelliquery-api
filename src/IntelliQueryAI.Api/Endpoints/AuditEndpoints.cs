using IntelliQueryAI.Api.Extensions;
using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Application.Features.Audit.Dtos;
using IntelliQueryAI.Application.Features.Audit.Queries;
using IntelliQueryAI.Domain.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace IntelliQueryAI.Api.Endpoints;

public static class AuditEndpoints
{
    public static IEndpointRouteBuilder MapAuditEndpoints(this IEndpointRouteBuilder app)
    {
        var groups = new[]
        {
            app.MapGroup("/api/audit-logs").WithTags("Audit").RequireAuthorization(),
            app.MapGroup("/audit-logs").WithTags("Audit").RequireAuthorization()
        };

        foreach (var group in groups)
        {
            // 1. GET /api/audit-logs?workspaceId={guid}&limit={int} (or from ITenantContext)
            group.MapGet("", async (
                [FromQuery] Guid? workspaceId,
                [FromQuery] int? limit,
                ITenantContext tenantContext,
                [FromServices] IDispatcher dispatcher,
                CancellationToken ct) =>
            {
                Guid? targetWorkspaceId = workspaceId ?? tenantContext.WorkspaceId;
                if (!targetWorkspaceId.HasValue || targetWorkspaceId.Value == Guid.Empty)
                {
                    return Results.Ok(Array.Empty<AuditLogDto>());
                }

                var query = new GetAuditLogsQuery(targetWorkspaceId.Value, limit ?? 50);
                var result = await dispatcher.QueryAsync(query, ct);
                return result.ToHttpResult();
            })
            .WithName($"GetAuditLogs_{group.GetHashCode()}")
            .WithSummary("Retrieves security and operations audit logs for a workspace via query parameter or tenant context");

            // 2. GET /api/audit-logs/{workspaceId:guid}?limit={int}
            group.MapGet("/{workspaceId:guid}", async (
                [FromRoute] Guid workspaceId,
                [FromQuery] int? limit,
                [FromServices] IDispatcher dispatcher,
                CancellationToken ct) =>
            {
                var query = new GetAuditLogsQuery(workspaceId, limit ?? 50);
                var result = await dispatcher.QueryAsync(query, ct);
                return result.ToHttpResult();
            })
            .WithName($"GetAuditLogsByRoute_{group.GetHashCode()}")
            .WithSummary("Retrieves security and operations audit logs for a workspace via route parameter");
        }

        return app;
    }
}
