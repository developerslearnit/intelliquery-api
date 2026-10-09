using System.Security.Claims;
using FluentAssertions;
using IntelliQueryAI.Api.Middleware;
using IntelliQueryAI.Domain.Entities.Workspaces;
using IntelliQueryAI.Domain.Enums;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.Domain.ValueObjects;
using IntelliQueryAI.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace IntelliQueryAI.IntegrationTests;

public class TenantResolutionMiddlewareTests
{
    private sealed class InMemoryWorkspaceStore : IWorkspaceStore
    {
        private readonly List<Workspace> _workspaces = [];

        public void Add(Workspace workspace) => _workspaces.Add(workspace);

        public Task<Workspace?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_workspaces.FirstOrDefault(w => w.Id == id));

        public Task<Workspace?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
            => Task.FromResult(_workspaces.FirstOrDefault(w => w.Slug.Value == slug));

        public Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken = default)
            => Task.FromResult(_workspaces.Any(w => w.Slug.Value == slug));

        public Task AddAsync(Workspace workspace, CancellationToken cancellationToken = default)
        {
            _workspaces.Add(workspace);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Workspace>> GetUserWorkspacesAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Workspace>>(_workspaces);

        public Task UpdateAsync(Workspace workspace, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    [Fact]
    public async Task InvokeAsync_WhenXTenantIdHeaderProvided_OverridesJwtClaims()
    {
        // Arrange
        var wsStore = new InMemoryWorkspaceStore();

        var ws1 = Workspace.Create("Workspace 1", TenantSlug.Create("ws-1").Value, SubscriptionPlan.Free, Guid.NewGuid()).Value;
        var ws2 = Workspace.Create("Workspace 2", TenantSlug.Create("ws-2").Value, SubscriptionPlan.Pro, Guid.NewGuid()).Value;
        wsStore.Add(ws1);
        wsStore.Add(ws2);

        var tenantContext = new TenantContext();
        var middleware = new TenantResolutionMiddleware(
            next: (ctx) => Task.CompletedTask,
            logger: NullLogger<TenantResolutionMiddleware>.Instance);

        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("localhost", 5000);

        // Header says Workspace 2
        context.Request.Headers["X-Tenant-Id"] = ws2.Id.ToString();

        // JWT Claims say Workspace 1
        var claims = new[]
        {
            new Claim("tenant_id", ws1.Id.ToString()),
            new Claim("tenant_slug", ws1.Slug.Value)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        context.User = new ClaimsPrincipal(identity);

        // Act
        await middleware.InvokeAsync(context, tenantContext, wsStore);

        // Assert
        tenantContext.IsResolved.Should().BeTrue();
        tenantContext.WorkspaceId.Should().Be(ws2.Id, "Explicit X-Tenant-Id header must override fallback JWT claims");
        tenantContext.Slug.Should().Be(ws2.Slug.Value);
    }

    [Fact]
    public async Task InvokeAsync_WhenNoHeadersProvided_FallsBackToJwtClaims()
    {
        // Arrange
        var wsStore = new InMemoryWorkspaceStore();

        var ws1 = Workspace.Create("Workspace 1", TenantSlug.Create("ws-1").Value, SubscriptionPlan.Free, Guid.NewGuid()).Value;
        wsStore.Add(ws1);

        var tenantContext = new TenantContext();
        var middleware = new TenantResolutionMiddleware(
            next: (ctx) => Task.CompletedTask,
            logger: NullLogger<TenantResolutionMiddleware>.Instance);

        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("localhost", 5000);

        // No headers provided
        // JWT Claims say Workspace 1
        var claims = new[]
        {
            new Claim("tenant_id", ws1.Id.ToString()),
            new Claim("tenant_slug", ws1.Slug.Value)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        context.User = new ClaimsPrincipal(identity);

        // Act
        await middleware.InvokeAsync(context, tenantContext, wsStore);

        // Assert
        tenantContext.IsResolved.Should().BeTrue();
        tenantContext.WorkspaceId.Should().Be(ws1.Id);
        tenantContext.Slug.Should().Be(ws1.Slug.Value);
    }
}
