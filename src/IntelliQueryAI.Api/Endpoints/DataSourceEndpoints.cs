using System.Security.Claims;
using IntelliQueryAI.Api.Extensions;
using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Application.Features.DataSources.Commands.RegisterDataSource;
using IntelliQueryAI.Application.Features.DataSources.Commands.SyncDataSource;
using IntelliQueryAI.Application.Features.DataSources.Commands.TestConnection;
using IntelliQueryAI.Application.Features.DataSources.Commands.ToggleDataSource;
using IntelliQueryAI.Application.Features.DataSources.Dtos;
using IntelliQueryAI.Application.Features.DataSources.Queries;
using IntelliQueryAI.Domain.Enums;
using IntelliQueryAI.Domain.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace IntelliQueryAI.Api.Endpoints;

public sealed record RegisterDataSourceRequest(
    string Name,
    DatabaseType DatabaseType,
    string ConnectionString,
    Guid? WorkspaceId = null);

public sealed record TestConnectionRequest(
    Guid? DataSourceId = null,
    DatabaseType? DatabaseType = null,
    string? ConnectionString = null);

public sealed record SyncDataSourceRequest(
    Guid DataSourceId,
    Guid? WorkspaceId = null);

public sealed record ToggleDataSourceRequest(
    bool Enable,
    Guid? WorkspaceId = null);

public static class DataSourceEndpoints
{
    public static IEndpointRouteBuilder MapDataSourceEndpoints(this IEndpointRouteBuilder app)
    {
        // Register under both /api/datasources and /datasources for routing flexibility
        var groups = new[]
        {
            app.MapGroup("/api/datasources").WithTags("Data Sources"),
            app.MapGroup("/datasources").WithTags("Data Sources")
        };

        foreach (var group in groups)
        {
            // 1. Register new database connection
            group.MapPost("/", async (
                [FromBody] RegisterDataSourceRequest request,
                ClaimsPrincipal user,
                ITenantContext tenantContext,
                [FromServices] IDispatcher dispatcher,
                CancellationToken ct) =>
            {
                Guid? workspaceId = request.WorkspaceId ?? tenantContext.WorkspaceId;
                if (!workspaceId.HasValue)
                {
                    return Results.BadRequest(new { error = "Workspace ID is required. Provide it via request body, X-Tenant-Id header, or tenant subdomain." });
                }

                string? sub = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
                Guid userId = Guid.TryParse(sub, out var parsedUserId) ? parsedUserId : Guid.Empty;

                var command = new RegisterDataSourceCommand(
                    workspaceId.Value,
                    request.Name,
                    request.DatabaseType,
                    request.ConnectionString,
                    userId);

                var result = await dispatcher.SendAsync(command, ct);
                return result.ToHttpResult(StatusCodes.Status201Created);
            })
            .RequireAuthorization()
            .WithName($"RegisterDataSource_{group.GetHashCode()}")
            .WithSummary("Registers and tests a new external database connection for the workspace");

            // 2. List data sources for a workspace
            group.MapGet("/", async (
                [FromQuery] Guid? workspaceId,
                ITenantContext tenantContext,
                [FromServices] IDispatcher dispatcher,
                CancellationToken ct) =>
            {
                Guid? targetWorkspaceId = workspaceId ?? tenantContext.WorkspaceId;
                if (!targetWorkspaceId.HasValue)
                {
                    return Results.BadRequest(new { error = "Workspace ID is required. Provide it via query param, X-Tenant-Id header, or tenant subdomain." });
                }

                var query = new GetDataSourcesQuery(targetWorkspaceId.Value);
                var result = await dispatcher.QueryAsync(query, ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization()
            .WithName($"GetDataSources_{group.GetHashCode()}")
            .WithSummary("Lists all registered data sources for the current workspace");

            // 3. Get single data source by ID
            group.MapGet("/{id:guid}", async (
                [FromRoute] Guid id,
                [FromQuery] Guid? workspaceId,
                ITenantContext tenantContext,
                [FromServices] IDispatcher dispatcher,
                CancellationToken ct) =>
            {
                Guid? targetWorkspaceId = workspaceId ?? tenantContext.WorkspaceId;
                if (!targetWorkspaceId.HasValue)
                {
                    return Results.BadRequest(new { error = "Workspace ID is required. Provide it via query param, X-Tenant-Id header, or tenant subdomain." });
                }

                var query = new GetDataSourceByIdQuery(id, targetWorkspaceId.Value);
                var result = await dispatcher.QueryAsync(query, ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization()
            .WithName($"GetDataSourceById_{group.GetHashCode()}")
            .WithSummary("Retrieves metadata details of a specific registered data source");

            // 4. Test connectivity
            group.MapPost("/test", async (
                [FromBody] TestConnectionRequest request,
                [FromServices] IDispatcher dispatcher,
                CancellationToken ct) =>
            {
                var command = new TestConnectionCommand(
                    request.DataSourceId,
                    request.DatabaseType,
                    request.ConnectionString);

                var result = await dispatcher.SendAsync(command, ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization()
            .WithName($"TestConnection_{group.GetHashCode()}")
            .WithSummary("Tests database connectivity for existing data source or raw connection credentials");

            // 5. Trigger immediate schema synchronization (POST /datasources/sync)
            group.MapPost("/sync", async (
                [FromBody] SyncDataSourceRequest request,
                ClaimsPrincipal user,
                ITenantContext tenantContext,
                [FromServices] IDispatcher dispatcher,
                CancellationToken ct) =>
            {
                Guid? workspaceId = request.WorkspaceId ?? tenantContext.WorkspaceId;
                if (!workspaceId.HasValue)
                {
                    return Results.BadRequest(new { error = "Workspace ID is required." });
                }

                string? sub = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
                Guid? userId = Guid.TryParse(sub, out var parsedUserId) ? parsedUserId : null;

                var command = new SyncDataSourceCommand(request.DataSourceId, workspaceId.Value, userId);
                var result = await dispatcher.SendAsync(command, ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization()
            .WithName($"SyncDataSource_{group.GetHashCode()}")
            .WithSummary("Initiates an immediate schema discovery scan and metadata ingestion for a data source");

            // 5b. Trigger immediate schema synchronization by ID route (POST /datasources/{id}/sync)
            group.MapPost("/{id:guid}/sync", async (
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
                Guid? userId = Guid.TryParse(sub, out var parsedUserId) ? parsedUserId : null;

                var command = new SyncDataSourceCommand(id, targetWorkspaceId.Value, userId);
                var result = await dispatcher.SendAsync(command, ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization()
            .WithName($"SyncDataSourceById_{group.GetHashCode()}")
            .WithSummary("Initiates an immediate schema discovery scan and metadata ingestion by route ID");

            // 6. Enable or disable data source
            group.MapPut("/{id:guid}/toggle", async (
                [FromRoute] Guid id,
                [FromBody] ToggleDataSourceRequest request,
                ITenantContext tenantContext,
                [FromServices] IDispatcher dispatcher,
                CancellationToken ct) =>
            {
                Guid? targetWorkspaceId = request.WorkspaceId ?? tenantContext.WorkspaceId;
                if (!targetWorkspaceId.HasValue)
                {
                    return Results.BadRequest(new { error = "Workspace ID is required." });
                }

                var command = new ToggleDataSourceCommand(id, request.Enable, targetWorkspaceId.Value);
                var result = await dispatcher.SendAsync(command, ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization()
            .WithName($"ToggleDataSource_{group.GetHashCode()}")
            .WithSummary("Enables or disables connectivity and query execution for a registered data source");
        }

        return app;
    }
}
