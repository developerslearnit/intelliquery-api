using System.Security.Claims;
using IntelliQueryAI.Domain.Entities.Users;
using IntelliQueryAI.Domain.Entities.Workspaces;

namespace IntelliQueryAI.Domain.Services;

/// <summary>
/// Scoped tenant context resolved per request from subdomain, header, or JWT claims.
/// </summary>
public interface ITenantContext
{
    /// <summary>
    /// Gets the resolved workspace tenant identifier, or null if unassigned/public.
    /// </summary>
    Guid? WorkspaceId { get; }

    /// <summary>
    /// Gets the resolved workspace tenant slug, or null.
    /// </summary>
    string? Slug { get; }

    /// <summary>
    /// Gets a value indicating whether a tenant context was successfully resolved for this request.
    /// </summary>
    bool IsResolved { get; }

    /// <summary>
    /// Sets the tenant context for the current request scope.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="slug">The workspace slug.</param>
    void SetTenant(Guid workspaceId, string slug);
}

/// <summary>
/// Service abstraction for secure password hashing and verification.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    /// Generates a secure salted hash of the plaintext password.
    /// </summary>
    string Hash(string password);

    /// <summary>
    /// Verifies whether the plaintext password matches the stored hash.
    /// </summary>
    bool Verify(string password, string passwordHash);
}

/// <summary>
/// Service abstraction for generating JWT access tokens and secure refresh tokens.
/// </summary>
public interface IJwtTokenGenerator
{
    /// <summary>
    /// Generates a signed JWT access token containing standard identity and role claims.
    /// </summary>
    string GenerateAccessToken(User user, Workspace? currentWorkspace = null, string? workspaceRole = null);

    /// <summary>
    /// Generates a cryptographically strong random refresh token string.
    /// </summary>
    string GenerateRefreshToken();
}

/// <summary>
/// Time abstraction for testability.
/// </summary>
public interface IDateTimeProvider
{
    /// <summary>
    /// Gets the current date and time in UTC.
    /// </summary>
    DateTime UtcNow { get; }
}
