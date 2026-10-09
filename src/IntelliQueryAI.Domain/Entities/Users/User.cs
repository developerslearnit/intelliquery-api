using IntelliQueryAI.Domain.Enums;
using IntelliQueryAI.Domain.Errors;
using IntelliQueryAI.Domain.ValueObjects;
using IntelliQueryAI.SharedKernel.Primitives;
using IntelliQueryAI.SharedKernel.Results;

namespace IntelliQueryAI.Domain.Entities.Users;

/// <summary>
/// Aggregate root representing a system user with authentication credentials and system role.
/// </summary>
public sealed class User : AggregateRoot
{
    /// <summary>
    /// Gets the unique normalized email address.
    /// </summary>
    public EmailAddress Email { get; private set; } = null!;

    /// <summary>
    /// Gets the BCrypt hashed password.
    /// </summary>
    public string PasswordHash { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the user's first name.
    /// </summary>
    public string FirstName { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the user's last name.
    /// </summary>
    public string LastName { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the global system role.
    /// </summary>
    public SystemRole SystemRole { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the user's email address has been verified.
    /// </summary>
    public bool IsEmailVerified { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the user account is active.
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Gets the creation timestamp in UTC.
    /// </summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>
    /// Gets the last updated timestamp in UTC.
    /// </summary>
    public DateTime? UpdatedAtUtc { get; private set; }

    private User(
        EmailAddress email,
        string passwordHash,
        string firstName,
        string lastName,
        SystemRole systemRole,
        bool isEmailVerified = false)
        : base(Guid.NewGuid())
    {
        Email = email;
        PasswordHash = passwordHash;
        FirstName = firstName;
        LastName = lastName;
        SystemRole = systemRole;
        IsEmailVerified = isEmailVerified;
        IsActive = true;
        CreatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Required for EF Core.
    /// </summary>
    private User()
    {
    }

    /// <summary>
    /// Creates a new User aggregate root.
    /// </summary>
    public static Result<User> Create(
        EmailAddress email,
        string passwordHash,
        string firstName,
        string lastName,
        SystemRole systemRole = SystemRole.User,
        bool isEmailVerified = false)
    {
        if (email is null)
        {
            return Result.Failure<User>(IdentityErrors.EmailRequired);
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            return Result.Failure<User>(IdentityErrors.WeakPassword);
        }

        var user = new User(email, passwordHash, firstName.Trim(), lastName.Trim(), systemRole, isEmailVerified);
        return Result.Success(user);
    }

    /// <summary>
    /// Marks the user's email as verified.
    /// </summary>
    public void VerifyEmail()
    {
        IsEmailVerified = true;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the user's hashed password.
    /// </summary>
    public void UpdatePassword(string newPasswordHash)
    {
        PasswordHash = newPasswordHash;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the user's profile details.
    /// </summary>
    public void UpdateProfile(string firstName, string lastName)
    {
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Deactivates the user account.
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Activates the user account.
    /// </summary>
    public void Activate()
    {
        IsActive = true;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
