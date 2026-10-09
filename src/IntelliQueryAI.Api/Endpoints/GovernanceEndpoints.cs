using System.Security.Claims;
using IntelliQueryAI.Api.Extensions;
using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Application.Features.Governance.Commands;
using IntelliQueryAI.Application.Features.Governance.Dtos;
using IntelliQueryAI.Application.Features.Governance.Queries;
using IntelliQueryAI.Domain.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace IntelliQueryAI.Api.Endpoints;

public static class GovernanceEndpoints
{
    public static IEndpointRouteBuilder MapGovernanceEndpoints(this IEndpointRouteBuilder app)
    {
        var groups = new[]
        {
            app.MapGroup("/api/governance").WithTags("Enterprise Data Governance").RequireAuthorization(),
            app.MapGroup("/governance").WithTags("Enterprise Data Governance").RequireAuthorization()
        };

        foreach (var group in groups)
        {
            // 1. GET /api/governance/policies - List governance policies for active workspace
            group.MapGet("/policies", async (
                [FromQuery] Guid? workspaceId,
                [FromQuery] bool? activeOnly,
                ITenantContext tenantContext,
                [FromServices] IDispatcher dispatcher,
                CancellationToken ct) =>
            {
                Guid? targetWorkspaceId = workspaceId ?? tenantContext.WorkspaceId;
                if (!targetWorkspaceId.HasValue)
                {
                    return Results.BadRequest(new { error = "Workspace ID is required." });
                }

                var query = new GetGovernancePoliciesQuery(targetWorkspaceId.Value, activeOnly ?? false);
                var result = await dispatcher.QueryAsync(query, ct);
                return result.ToHttpResult();
            })
            .WithName($"GetGovernancePolicies_{group.GetHashCode()}")
            .WithSummary("Retrieves all enterprise data governance policies for a workspace.");

            // 2. POST /api/governance/policies - Create a new governance policy
            group.MapPost("/policies", async (
                [FromBody] CreateGovernancePolicyRequestDto request,
                [FromQuery] Guid? workspaceId,
                ClaimsPrincipal user,
                ITenantContext tenantContext,
                [FromServices] IDispatcher dispatcher,
                CancellationToken ct) =>
            {
                Guid? targetWorkspaceId = workspaceId ?? tenantContext.WorkspaceId;
                if (!targetWorkspaceId.HasValue)
                {
                    return Results.BadRequest(new { error = "Workspace ID is required." });
                }

                string? sub = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
                Guid userId = Guid.TryParse(sub, out var parsedId) ? parsedId : Guid.Empty;

                var command = new CreateGovernancePolicyCommand(
                    WorkspaceId: targetWorkspaceId.Value,
                    UserId: userId,
                    Name: request.Name,
                    Description: request.Description,
                    TargetTable: request.TargetTable,
                    TargetColumn: request.TargetColumn,
                    Classification: request.Classification,
                    MaskingStrategy: request.MaskingStrategy,
                    MaskingPattern: request.MaskingPattern,
                    AllowedRoles: request.AllowedRoles,
                    IsActive: request.IsActive,
                    IsAiMaskingEnforced: request.IsAiMaskingEnforced);

                var result = await dispatcher.SendAsync(command, ct);
                return result.ToHttpResult();
            })
            .WithName($"CreateGovernancePolicy_{group.GetHashCode()}")
            .WithSummary("Creates an enterprise data governance policy with masking and role-based access rules.");

            // 3. GET /api/governance/policies/{id} - Get policy by ID
            group.MapGet("/policies/{id:guid}", async (
                [FromRoute] Guid id,
                [FromQuery] Guid? workspaceId,
                ITenantContext tenantContext,
                [FromServices] IDispatcher dispatcher,
                CancellationToken ct) =>
            {
                Guid? targetWorkspaceId = workspaceId ?? tenantContext.WorkspaceId;
                if (!targetWorkspaceId.HasValue)
                {
                    return Results.BadRequest(new { error = "Workspace ID is required." });
                }

                var query = new GetGovernancePolicyByIdQuery(targetWorkspaceId.Value, id);
                var result = await dispatcher.QueryAsync(query, ct);
                return result.ToHttpResult();
            })
            .WithName($"GetGovernancePolicyById_{group.GetHashCode()}")
            .WithSummary("Retrieves a specific governance policy by unique identifier.");

            // 4. PUT /api/governance/policies/{id} - Update policy
            group.MapPut("/policies/{id:guid}", async (
                [FromRoute] Guid id,
                [FromBody] UpdateGovernancePolicyRequestDto request,
                [FromQuery] Guid? workspaceId,
                ClaimsPrincipal user,
                ITenantContext tenantContext,
                [FromServices] IDispatcher dispatcher,
                CancellationToken ct) =>
            {
                Guid? targetWorkspaceId = workspaceId ?? tenantContext.WorkspaceId;
                if (!targetWorkspaceId.HasValue)
                {
                    return Results.BadRequest(new { error = "Workspace ID is required." });
                }

                string? sub = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
                Guid userId = Guid.TryParse(sub, out var parsedId) ? parsedId : Guid.Empty;

                var command = new UpdateGovernancePolicyCommand(
                    WorkspaceId: targetWorkspaceId.Value,
                    UserId: userId,
                    PolicyId: id,
                    Name: request.Name,
                    Description: request.Description,
                    TargetTable: request.TargetTable,
                    TargetColumn: request.TargetColumn,
                    Classification: request.Classification,
                    MaskingStrategy: request.MaskingStrategy,
                    MaskingPattern: request.MaskingPattern,
                    AllowedRoles: request.AllowedRoles,
                    IsActive: request.IsActive,
                    IsAiMaskingEnforced: request.IsAiMaskingEnforced);

                var result = await dispatcher.SendAsync(command, ct);
                return result.ToHttpResult();
            })
            .WithName($"UpdateGovernancePolicy_{group.GetHashCode()}")
            .WithSummary("Updates an existing enterprise data governance policy.");

            // 5. DELETE /api/governance/policies/{id} - Delete policy
            group.MapDelete("/policies/{id:guid}", async (
                [FromRoute] Guid id,
                [FromQuery] Guid? workspaceId,
                ClaimsPrincipal user,
                ITenantContext tenantContext,
                [FromServices] IDispatcher dispatcher,
                CancellationToken ct) =>
            {
                Guid? targetWorkspaceId = workspaceId ?? tenantContext.WorkspaceId;
                if (!targetWorkspaceId.HasValue)
                {
                    return Results.BadRequest(new { error = "Workspace ID is required." });
                }

                string? sub = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
                Guid userId = Guid.TryParse(sub, out var parsedId) ? parsedId : Guid.Empty;

                var command = new DeleteGovernancePolicyCommand(targetWorkspaceId.Value, userId, id);
                var result = await dispatcher.SendAsync(command, ct);
                return result.ToHttpResult();
            })
            .WithName($"DeleteGovernancePolicy_{group.GetHashCode()}")
            .WithSummary("Deletes an enterprise data governance policy.");

            // 6. POST /api/governance/policies/{id}/toggle - Toggle active status
            group.MapPost("/policies/{id:guid}/toggle", async (
                [FromRoute] Guid id,
                [FromBody] ToggleGovernancePolicyRequestDto? request,
                [FromQuery] Guid? workspaceId,
                ClaimsPrincipal user,
                ITenantContext tenantContext,
                [FromServices] IDispatcher dispatcher,
                CancellationToken ct) =>
            {
                Guid? targetWorkspaceId = workspaceId ?? tenantContext.WorkspaceId;
                if (!targetWorkspaceId.HasValue)
                {
                    return Results.BadRequest(new { error = "Workspace ID is required." });
                }

                string? sub = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
                Guid userId = Guid.TryParse(sub, out var parsedId) ? parsedId : Guid.Empty;

                var command = new ToggleGovernancePolicyCommand(
                    targetWorkspaceId.Value,
                    userId,
                    id,
                    request?.IsActive);

                var result = await dispatcher.SendAsync(command, ct);
                return result.ToHttpResult();
            })
            .WithName($"ToggleGovernancePolicy_{group.GetHashCode()}")
            .WithSummary("Toggles active state of a governance policy.");

            // 7. POST /api/governance/preview-mask - Interactive test playground for masking strategies
            group.MapPost("/preview-mask", async (
                [FromBody] TestMaskingPreviewRequestDto request,
                [FromServices] IDispatcher dispatcher,
                CancellationToken ct) =>
            {
                var query = new TestMaskingPreviewQuery(
                    request.SampleValue,
                    request.MaskingStrategy,
                    request.MaskingPattern);

                var result = await dispatcher.QueryAsync(query, ct);
                return result.ToHttpResult();
            })
            .WithName($"TestMaskingPreview_{group.GetHashCode()}")
            .WithSummary("Interactive sandbox to preview masking rules against sample data before applying to schema.");

            // 8. GET /api/governance/audit - Sensitive data access audit logs
            group.MapGet("/audit", async (
                [FromQuery] Guid? workspaceId,
                [FromQuery] int? limit,
                ITenantContext tenantContext,
                [FromServices] IDispatcher dispatcher,
                CancellationToken ct) =>
            {
                Guid? targetWorkspaceId = workspaceId ?? tenantContext.WorkspaceId;
                if (!targetWorkspaceId.HasValue)
                {
                    return Results.BadRequest(new { error = "Workspace ID is required." });
                }

                var query = new GetGovernanceAuditLogsQuery(targetWorkspaceId.Value, limit ?? 50);
                var result = await dispatcher.QueryAsync(query, ct);
                return result.ToHttpResult();
            })
            .WithName($"GetGovernanceAuditLogs_{group.GetHashCode()}")
            .WithSummary("Retrieves audit trail of sensitive data governance access and policy modifications.");
        }

        return app;
    }
}
