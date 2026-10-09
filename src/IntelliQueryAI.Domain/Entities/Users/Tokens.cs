using IntelliQueryAI.SharedKernel.Primitives;

namespace IntelliQueryAI.Domain.Entities.Users;

/// <summary>
/// Represents a cryptographically secure refresh token for JWT token rotation.
/// </summary>
public sealed class RefreshToken : Entity
{
    /// <summary>
    /// Gets the identifier of the associated user.
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>
    /// Gets the opaque token string.
    /// </summary>
    public string Token { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the token expiration date and time in UTC.
    /// </summary>
    public DateTime ExpiresAtUtc { get; private set; }

    /// <summary>
    /// Gets the creation timestamp in UTC.
    /// </summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>
    /// Gets the timestamp when this token was revoked, if any.
    /// </summary>
    public DateTime? RevokedAtUtc { get; private set; }

    /// <summary>
    /// Gets the token that replaced this one during a rotation event.
    /// </summary>
    public string? ReplacedByToken { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the token has expired.
    /// </summary>
    public bool IsExpired => DateTime.UtcNow >= ExpiresAtUtc;

    /// <summary>
    /// Gets a value indicating whether the token has been revoked.
    /// </summary>
    public bool IsRevoked => RevokedAtUtc.HasValue;

    /// <summary>
    /// Gets a value indicating whether the token is currently active and valid.
    /// </summary>
    public bool IsActive => !IsRevoked && !IsExpired;

    private RefreshToken(Guid userId, string token, DateTime expiresAtUtc)
        : base(Guid.NewGuid())
    {
        UserId = userId;
        Token = token;
        ExpiresAtUtc = expiresAtUtc;
        CreatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Required for EF Core.
    /// </summary>
    private RefreshToken()
    {
    }

    /// <summary>
    /// Creates a new refresh token instance.
    /// </summary>
    public static RefreshToken Create(Guid userId, string token, DateTime expiresAtUtc)
    {
        return new RefreshToken(userId, token, expiresAtUtc);
    }

    /// <summary>
    /// Marks the refresh token as revoked.
    /// </summary>
    /// <param name="replacedByToken">The new token generated in rotation, if applicable.</param>
    public void Revoke(string? replacedByToken = null)
    {
        RevokedAtUtc = DateTime.UtcNow;
        ReplacedByToken = replacedByToken;
    }
}

/// <summary>
/// Represents a one-time token for verifying user email addresses.
/// </summary>
public sealed class EmailVerificationToken : Entity
{
    public Guid UserId { get; private set; }
    public string Token { get; private set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UsedAtUtc { get; private set; }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAtUtc;
    public bool IsUsed => UsedAtUtc.HasValue;
    public bool IsValid => !IsUsed && !IsExpired;

    private EmailVerificationToken(Guid userId, string token, DateTime expiresAtUtc)
        : base(Guid.NewGuid())
    {
        UserId = userId;
        Token = token;
        ExpiresAtUtc = expiresAtUtc;
        CreatedAtUtc = DateTime.UtcNow;
    }

    private EmailVerificationToken() { }

    public static EmailVerificationToken Create(Guid userId, string token, TimeSpan validity)
    {
        return new EmailVerificationToken(userId, token, DateTime.UtcNow.Add(validity));
    }

    public void MarkAsUsed()
    {
        UsedAtUtc = DateTime.UtcNow;
    }
}

/// <summary>
/// Represents a one-time token for resetting account passwords.
/// </summary>
public sealed class PasswordResetToken : Entity
{
    public Guid UserId { get; private set; }
    public string Token { get; private set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UsedAtUtc { get; private set; }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAtUtc;
    public bool IsUsed => UsedAtUtc.HasValue;
    public bool IsValid => !IsUsed && !IsExpired;

    private PasswordResetToken(Guid userId, string token, DateTime expiresAtUtc)
        : base(Guid.NewGuid())
    {
        UserId = userId;
        Token = token;
        ExpiresAtUtc = expiresAtUtc;
        CreatedAtUtc = DateTime.UtcNow;
    }

    private PasswordResetToken() { }

    public static PasswordResetToken Create(Guid userId, string token, TimeSpan validity)
    {
        return new PasswordResetToken(userId, token, DateTime.UtcNow.Add(validity));
    }

    public void MarkAsUsed()
    {
        UsedAtUtc = DateTime.UtcNow;
    }
}
