using IntelliQueryAI.Domain.Entities.Audit;
using IntelliQueryAI.Domain.Entities.Subscriptions;
using IntelliQueryAI.Domain.Entities.Users;
using IntelliQueryAI.Domain.Entities.Workspaces;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace IntelliQueryAI.Infrastructure.Persistence.Stores;

public sealed class EfWorkspaceStore : IWorkspaceStore
{
    private readonly IntelliQueryDbContext _context;

    public EfWorkspaceStore(IntelliQueryDbContext context)
    {
        _context = context;
    }

    public async Task<Workspace?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Workspaces
            .Include(w => w.Members)
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
    }

    public async Task<Workspace?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        var slugVo = TenantSlug.Create(slug).Value;
        return await _context.Workspaces
            .Include(w => w.Members)
            .FirstOrDefaultAsync(w => w.Slug == slugVo, cancellationToken);
    }

    public async Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken = default)
    {
        var slugVo = TenantSlug.Create(slug).Value;
        return await _context.Workspaces
            .AnyAsync(w => w.Slug == slugVo, cancellationToken);
    }

    public async Task AddAsync(Workspace workspace, CancellationToken cancellationToken = default)
    {
        await _context.Workspaces.AddAsync(workspace, cancellationToken);
    }

    public async Task<IReadOnlyList<Workspace>> GetUserWorkspacesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Workspaces
            .Include(w => w.Members)
            .Where(w => w.Members.Any(m => m.UserId == userId))
            .ToListAsync(cancellationToken);
    }
}

public sealed class EfUserStore : IUserStore
{
    private readonly IntelliQueryDbContext _context;

    public EfUserStore(IntelliQueryDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var emailVo = EmailAddress.Create(email).Value;
        return await _context.Users.FirstOrDefaultAsync(u => u.Email == emailVo, cancellationToken);
    }

    public async Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default)
    {
        var emailVo = EmailAddress.Create(email).Value;
        return await _context.Users.AnyAsync(u => u.Email == emailVo, cancellationToken);
    }

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        await _context.Users.AddAsync(user, cancellationToken);
    }

    public async Task<EmailVerificationToken?> GetVerificationTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        return await _context.EmailVerificationTokens.FirstOrDefaultAsync(t => t.Token == token, cancellationToken);
    }

    public async Task AddVerificationTokenAsync(EmailVerificationToken token, CancellationToken cancellationToken = default)
    {
        await _context.EmailVerificationTokens.AddAsync(token, cancellationToken);
    }

    public async Task<PasswordResetToken?> GetPasswordResetTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        return await _context.PasswordResetTokens.FirstOrDefaultAsync(t => t.Token == token, cancellationToken);
    }

    public async Task AddPasswordResetTokenAsync(PasswordResetToken token, CancellationToken cancellationToken = default)
    {
        await _context.PasswordResetTokens.AddAsync(token, cancellationToken);
    }
}

public sealed class EfRefreshTokenStore : IRefreshTokenStore
{
    private readonly IntelliQueryDbContext _context;

    public EfRefreshTokenStore(IntelliQueryDbContext context)
    {
        _context = context;
    }

    public async Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        return await _context.RefreshTokens.FirstOrDefaultAsync(r => r.Token == token, cancellationToken);
    }

    public async Task AddAsync(RefreshToken token, CancellationToken cancellationToken = default)
    {
        await _context.RefreshTokens.AddAsync(token, cancellationToken);
    }

    public async Task RevokeUserTokensAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var activeTokens = await _context.RefreshTokens
            .Where(r => r.UserId == userId && r.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.Revoke();
        }
    }
}

public sealed class EfSubscriptionStore : ISubscriptionStore
{
    private readonly IntelliQueryDbContext _context;

    public EfSubscriptionStore(IntelliQueryDbContext context)
    {
        _context = context;
    }

    public async Task<Subscription?> GetByWorkspaceIdAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        return await _context.Subscriptions.FirstOrDefaultAsync(s => s.WorkspaceId == workspaceId, cancellationToken);
    }

    public async Task AddAsync(Subscription subscription, CancellationToken cancellationToken = default)
    {
        await _context.Subscriptions.AddAsync(subscription, cancellationToken);
    }
}

public sealed class EfAuditLogStore : IAuditLogStore
{
    private readonly IntelliQueryDbContext _context;

    public EfAuditLogStore(IntelliQueryDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(AuditLog log, CancellationToken cancellationToken = default)
    {
        await _context.AuditLogs.AddAsync(log, cancellationToken);
    }

    public async Task<IReadOnlyList<AuditLog>> GetByWorkspaceAsync(Guid workspaceId, int limit = 50, CancellationToken cancellationToken = default)
    {
        return await _context.AuditLogs
            .Where(a => a.WorkspaceId == workspaceId)
            .OrderByDescending(a => a.TimestampUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }
}

public sealed class EfUnitOfWork : IUnitOfWork
{
    private readonly IntelliQueryDbContext _context;

    public EfUnitOfWork(IntelliQueryDbContext context)
    {
        _context = context;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }
}
