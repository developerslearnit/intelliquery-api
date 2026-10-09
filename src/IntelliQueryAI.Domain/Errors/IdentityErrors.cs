using IntelliQueryAI.SharedKernel.Results;

namespace IntelliQueryAI.Domain.Errors;

/// <summary>
/// Domain errors related to Identity, Authentication, and User management.
/// </summary>
public static class IdentityErrors
{
    public static readonly Error EmailRequired =
        Error.Validation("Identity.EmailRequired", "Email address is required.");

    public static Error EmailTooLong(int max) =>
        Error.Validation("Identity.EmailTooLong", $"Email address cannot exceed {max} characters.");

    public static readonly Error EmailInvalidFormat =
        Error.Validation("Identity.EmailInvalidFormat", "Email address format is invalid.");

    public static readonly Error EmailAlreadyInUse =
        Error.Conflict("Identity.EmailAlreadyInUse", "A user with this email address already exists.");

    public static readonly Error InvalidCredentials =
        Error.Unauthorized("Identity.InvalidCredentials", "Invalid email address or password.");

    public static readonly Error UserNotFound =
        Error.NotFound("Identity.UserNotFound", "User not found.");

    public static readonly Error UserInactive =
        Error.Forbidden("Identity.UserInactive", "User account is currently deactivated.");

    public static readonly Error EmailNotVerified =
        Error.Forbidden("Identity.EmailNotVerified", "Email address has not been verified yet.");

    public static readonly Error InvalidRefreshToken =
        Error.Unauthorized("Identity.InvalidRefreshToken", "The refresh token is invalid, expired, or has been revoked.");

    public static readonly Error InvalidVerificationToken =
        Error.Validation("Identity.InvalidVerificationToken", "The email verification token is invalid or expired.");

    public static readonly Error InvalidResetToken =
        Error.Validation("Identity.InvalidResetToken", "The password reset token is invalid or expired.");

    public static readonly Error WeakPassword =
        Error.Validation("Identity.WeakPassword", "Password must be at least 8 characters long and contain uppercase, lowercase, numbers, and special characters.");
}
