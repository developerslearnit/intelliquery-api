using System.Security.Claims;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.Domain.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace IntelliQueryAI.Api.Middleware;

/// <summary>
/// Middleware responsible for resolving tenant workspace context from Subdomain, HTTP Header, or JWT Claims.
/// Supports multi-tenant subdomain resolution such as 'acme.aiquery.com' or 'globex.aiquery.com'.
/// </summary>
public sealed class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantResolutionMiddleware> _logger;

    public TenantResolutionMiddleware(
        RequestDelegate next,
        ILogger<TenantResolutionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ITenantContext tenantContext,
        IWorkspaceStore workspaceStore)
    {
        string? tenantSlug = null;
        Guid? tenantId = null;

        // 1. Check Subdomain (e.g. acme.aiquery.com or acme.localhost)
        string host = context.Request.Host.Host;
        string[] parts = host.Split('.');
        if (parts.Length >= 2)
        {
            string potentialSubdomain = parts[0].ToLowerInvariant();
            if (!string.Equals(potentialSubdomain, "www", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(potentialSubdomain, "api", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(potentialSubdomain, "localhost", StringComparison.OrdinalIgnoreCase))
            {
                tenantSlug = potentialSubdomain;
            }
        }

        // 2. Check X-Tenant-Slug header (highest priority header override)
        if (context.Request.Headers.TryGetValue("X-Tenant-Slug", out var headerSlug) && !string.IsNullOrWhiteSpace(headerSlug))
        {
            tenantSlug = headerSlug.ToString().Trim().ToLowerInvariant();
        }

        // 3. Check X-Tenant-Id header
        if (context.Request.Headers.TryGetValue("X-Tenant-Id", out var headerId) && Guid.TryParse(headerId, out var parsedHeaderId))
        {
            tenantId = parsedHeaderId;
        }

        // 4. Fallback to JWT Claims ONLY if neither an explicit header nor subdomain was provided
        if (!tenantId.HasValue && string.IsNullOrWhiteSpace(tenantSlug) && context.User.Identity?.IsAuthenticated == true)
        {
            if (Guid.TryParse(context.User.FindFirstValue("tenant_id"), out var claimId))
            {
                tenantId = claimId;
            }
            else
            {
                tenantSlug = context.User.FindFirstValue("tenant_slug");
            }
        }

        // 5. Query store and populate ITenantContext
        if (tenantId.HasValue)
        {
            var workspace = await workspaceStore.GetByIdAsync(tenantId.Value, context.RequestAborted);
            if (workspace is not null && workspace.IsActive)
            {
                tenantContext.SetTenant(workspace.Id, workspace.Slug.Value);
                context.Items["WorkspaceId"] = workspace.Id;
                context.Items["TenantSlug"] = workspace.Slug.Value;
                _logger.LogDebug("Resolved tenant {Slug} (Id: {Id}) via ID", workspace.Slug.Value, workspace.Id);
            }
        }

        if (!tenantContext.IsResolved && !string.IsNullOrWhiteSpace(tenantSlug))
        {
            var workspace = await workspaceStore.GetBySlugAsync(tenantSlug, context.RequestAborted);
            if (workspace is not null && workspace.IsActive)
            {
                tenantContext.SetTenant(workspace.Id, workspace.Slug.Value);
                context.Items["WorkspaceId"] = workspace.Id;
                context.Items["TenantSlug"] = workspace.Slug.Value;
                _logger.LogDebug("Resolved tenant {Slug} (Id: {Id}) via Slug", workspace.Slug.Value, workspace.Id);
            }
        }

        await _next(context);
    }
}
