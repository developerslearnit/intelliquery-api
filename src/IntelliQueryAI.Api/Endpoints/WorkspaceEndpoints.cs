using System.Security.Claims;
using IntelliQueryAI.Api.Extensions;
using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Application.Features.Workspaces.Commands.AddWorkspaceMember;
using IntelliQueryAI.Application.Features.Workspaces.Commands.CreateWorkspace;
using IntelliQueryAI.Application.Features.Workspaces.Queries;
using IntelliQueryAI.Domain.Enums;
using IntelliQueryAI.Domain.ValueObjects;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

using System.Text.Json.Serialization;

namespace IntelliQueryAI.Api.Endpoints;

public sealed record CreateWorkspaceRequest(
    string Name,
    string? Slug = null,
    [property: JsonConverter(typeof(JsonStringEnumConverter))]
    SubscriptionPlan Plan = SubscriptionPlan.Free);

public sealed record AddMemberRequest(
    string Email,
    [property: JsonConverter(typeof(JsonStringEnumConverter))]
    WorkspaceRole Role);

public static class WorkspaceEndpoints
{
    public static IEndpointRouteBuilder MapWorkspaceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/workspaces").WithTags("Workspaces");

        // Create new workspace
        group.MapPost("/", async (
            [FromBody] CreateWorkspaceRequest request,
            ClaimsPrincipal user,
            [FromServices] IDispatcher dispatcher,
            CancellationToken ct) =>
        {
            string? sub = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
            if (!Guid.TryParse(sub, out var userId))
            {
                return Results.Unauthorized();
            }

            string effectiveSlug = !string.IsNullOrWhiteSpace(request.Slug)
                ? request.Slug.Trim()
                : TenantSlug.Generate(request.Name);

            var command = new CreateWorkspaceCommand(userId, request.Name, effectiveSlug, request.Plan);
            var result = await dispatcher.SendAsync(command, ct);
            return result.ToHttpResult(StatusCodes.Status201Created);
        })
        .RequireAuthorization()
        .WithName("CreateWorkspace")
        .WithSummary("Creates a new tenant workspace");

        // Get current user's workspaces
        group.MapGet("/me", async (
            ClaimsPrincipal user,
            [FromServices] IDispatcher dispatcher,
            CancellationToken ct) =>
        {
            string? sub = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
            if (!Guid.TryParse(sub, out var userId))
            {
                return Results.Unauthorized();
            }

            var query = new GetUserWorkspacesQuery(userId);
            var result = await dispatcher.QueryAsync(query, ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization()
        .WithName("GetUserWorkspaces")
        .WithSummary("Retrieves all workspaces the current user belongs to");

        // Get workspace by ID
        group.MapGet("/{id:guid}", async (
            [FromRoute] Guid id,
            [FromServices] IDispatcher dispatcher,
            CancellationToken ct) =>
        {
            var query = new GetWorkspaceByIdQuery(id);
            var result = await dispatcher.QueryAsync(query, ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization()
        .WithName("GetWorkspaceById")
        .WithSummary("Retrieves a workspace by its unique identifier");

        // Get workspace by Slug (public verification / tenant resolution)
        group.MapGet("/slug/{slug}", async (
            [FromRoute] string slug,
            [FromServices] IDispatcher dispatcher,
            CancellationToken ct) =>
        {
            var query = new GetWorkspaceBySlugQuery(slug);
            var result = await dispatcher.QueryAsync(query, ct);
            return result.ToHttpResult();
        })
        .WithName("GetWorkspaceBySlug")
        .WithSummary("Retrieves a workspace by its tenant slug (e.g. acme for acme.aiquery.com)");

        // Add member to workspace
        group.MapPost("/{id:guid}/members", async (
            [FromRoute] Guid id,
            [FromBody] AddMemberRequest request,
            ClaimsPrincipal user,
            [FromServices] IDispatcher dispatcher,
            CancellationToken ct) =>
        {
            string? sub = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
            if (!Guid.TryParse(sub, out var userId))
            {
                return Results.Unauthorized();
            }

            var command = new AddWorkspaceMemberCommand(id, request.Email, request.Role, userId);
            var result = await dispatcher.SendAsync(command, ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization()
        .WithName("AddWorkspaceMember")
        .WithSummary("Adds a member to an existing workspace");

        return app;
    }
}
