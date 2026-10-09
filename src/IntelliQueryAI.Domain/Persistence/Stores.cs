using IntelliQueryAI.Domain.Entities.Audit;
using IntelliQueryAI.Domain.Entities.Subscriptions;
using IntelliQueryAI.Domain.Entities.Users;
using IntelliQueryAI.Domain.Entities.Workspaces;

namespace IntelliQueryAI.Domain.Persistence;

/// <summary>
/// Domain-owned contract for managing Workspace aggregate persistence.
/// </summary>
public interface IWorkspaceStore
{
    Task<Workspace?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Workspace?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);
    Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken = default);
    Task AddAsync(Workspace workspace, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Workspace>> GetUserWorkspacesAsync(Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Domain-owned contract for managing User aggregate persistence.
/// </summary>
public interface IUserStore
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);
    Task AddAsync(User user, CancellationToken cancellationToken = default);
    Task<EmailVerificationToken?> GetVerificationTokenAsync(string token, CancellationToken cancellationToken = default);
    Task AddVerificationTokenAsync(EmailVerificationToken token, CancellationToken cancellationToken = default);
    Task<PasswordResetToken?> GetPasswordResetTokenAsync(string token, CancellationToken cancellationToken = default);
    Task AddPasswordResetTokenAsync(PasswordResetToken token, CancellationToken cancellationToken = default);
}

/// <summary>
/// Domain-owned contract for managing Refresh Token rotation.
/// </summary>
public interface IRefreshTokenStore
{
    Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken cancellationToken = default);
    Task AddAsync(RefreshToken token, CancellationToken cancellationToken = default);
    Task RevokeUserTokensAsync(Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Domain-owned contract for managing Subscription aggregate persistence.
/// </summary>
public interface ISubscriptionStore
{
    Task<Subscription?> GetByWorkspaceIdAsync(Guid workspaceId, CancellationToken cancellationToken = default);
    Task AddAsync(Subscription subscription, CancellationToken cancellationToken = default);
}

/// <summary>
/// Domain-owned contract for logging and retrieving tenant audit entries.
/// </summary>
public interface IAuditLogStore
{
    Task AddAsync(AuditLog log, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditLog>> GetByWorkspaceAsync(Guid workspaceId, int limit = 50, CancellationToken cancellationToken = default);
}

/// <summary>
/// Transactional boundary abstraction owned by Domain.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
