using IntelliQueryAI.Api.Extensions;
using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Application.Features.Schema.Queries;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.Domain.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace IntelliQueryAI.Api.Endpoints;

public static class SchemaEndpoints
{
    public static IEndpointRouteBuilder MapSchemaEndpoints(this IEndpointRouteBuilder app)
    {
        // Support both /api/schema and /schema routes
        var groups = new[]
        {
            app.MapGroup("/api/schema").WithTags("Schema Metadata"),
            app.MapGroup("/schema").WithTags("Schema Metadata")
        };

        foreach (var group in groups)
        {
            // 1. Get full schema columns and attributes
            group.MapGet("/", async (
                [FromQuery] Guid? workspaceId,
                [FromQuery] Guid? dataSourceId,
                [FromQuery] string? tableName,
                ITenantContext tenantContext,
                [FromServices] IDispatcher dispatcher,
                [FromServices] IDataSourceStore dataSourceStore,
                CancellationToken ct) =>
            {
                Guid? targetWorkspaceId = workspaceId ?? tenantContext.WorkspaceId;
                if (!targetWorkspaceId.HasValue && dataSourceId.HasValue)
                {
                    var ds = await dataSourceStore.GetByIdAsync(dataSourceId.Value, ct);
                    if (ds is not null)
                    {
                        targetWorkspaceId = ds.WorkspaceId;
                    }
                }

                if (!targetWorkspaceId.HasValue)
                {
                    return Results.BadRequest(new { error = "Workspace ID is required. Provide it via query param, X-Tenant-Id header, or tenant subdomain." });
                }

                var query = new GetFullSchemaQuery(targetWorkspaceId.Value, dataSourceId, tableName);
                var result = await dispatcher.QueryAsync(query, ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization()
            .WithName($"GetFullSchema_{group.GetHashCode()}")
            .WithSummary("Retrieves all schema columns, types, nullability, and business-friendly aliases");

            // 2. Get distinct table summaries
            group.MapGet("/tables", async (
                [FromQuery] Guid? workspaceId,
                [FromQuery] Guid? dataSourceId,
                ITenantContext tenantContext,
                [FromServices] IDispatcher dispatcher,
                [FromServices] IDataSourceStore dataSourceStore,
                CancellationToken ct) =>
            {
                Guid? targetWorkspaceId = workspaceId ?? tenantContext.WorkspaceId;
                if (!targetWorkspaceId.HasValue && dataSourceId.HasValue)
                {
                    var ds = await dataSourceStore.GetByIdAsync(dataSourceId.Value, ct);
                    if (ds is not null)
                    {
                        targetWorkspaceId = ds.WorkspaceId;
                    }
                }

                if (!targetWorkspaceId.HasValue)
                {
                    return Results.BadRequest(new { error = "Workspace ID is required. Provide it via query param, X-Tenant-Id header, or tenant subdomain." });
                }

                var query = new GetSchemaTablesQuery(targetWorkspaceId.Value, dataSourceId);
                var result = await dispatcher.QueryAsync(query, ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization()
            .WithName($"GetSchemaTables_{group.GetHashCode()}")
            .WithSummary("Retrieves high-level table summaries with column counts and human-readable aliases");

            // 3. Get foreign key relationships
            group.MapGet("/relationships", async (
                [FromQuery] Guid? workspaceId,
                [FromQuery] Guid? dataSourceId,
                ITenantContext tenantContext,
                [FromServices] IDispatcher dispatcher,
                [FromServices] IDataSourceStore dataSourceStore,
                CancellationToken ct) =>
            {
                Guid? targetWorkspaceId = workspaceId ?? tenantContext.WorkspaceId;
                if (!targetWorkspaceId.HasValue && dataSourceId.HasValue)
                {
                    var ds = await dataSourceStore.GetByIdAsync(dataSourceId.Value, ct);
                    if (ds is not null)
                    {
                        targetWorkspaceId = ds.WorkspaceId;
                    }
                }

                if (!targetWorkspaceId.HasValue)
                {
                    return Results.BadRequest(new { error = "Workspace ID is required. Provide it via query param, X-Tenant-Id header, or tenant subdomain." });
                }

                var query = new GetSchemaRelationshipsQuery(targetWorkspaceId.Value, dataSourceId);
                var result = await dispatcher.QueryAsync(query, ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization()
            .WithName($"GetSchemaRelationships_{group.GetHashCode()}")
            .WithSummary("Retrieves primary-to-foreign key relationships for database entity graphing");
        }

        return app;
    }
}
