using IntelliQueryAI.SharedKernel.Results;

namespace IntelliQueryAI.Domain.Errors;

/// <summary>
/// Domain errors related to Workspace management and tenant operations.
/// </summary>
public static class WorkspaceErrors
{
    public static readonly Error NameRequired =
        Error.Validation("Workspace.NameRequired", "Workspace name is required.");

    public static readonly Error SlugRequired =
        Error.Validation("Workspace.SlugRequired", "Workspace slug is required.");

    public static Error SlugInvalidLength(int min, int max) =>
        Error.Validation("Workspace.SlugInvalidLength", $"Workspace slug must be between {min} and {max} characters.");

    public static readonly Error SlugInvalidFormat =
        Error.Validation("Workspace.SlugInvalidFormat", "Workspace slug may only contain lowercase letters, numbers, and single hyphens.");

    public static Error SlugReserved(string slug) =>
        Error.Validation("Workspace.SlugReserved", $"The slug '{slug}' is reserved by the platform and cannot be used.");

    public static readonly Error SlugAlreadyExists =
        Error.Conflict("Workspace.SlugAlreadyExists", "A workspace with this unique slug already exists.");

    public static readonly Error NotFound =
        Error.NotFound("Workspace.NotFound", "The specified workspace was not found.");

    public static readonly Error Inactive =
        Error.Forbidden("Workspace.Inactive", "This workspace is currently inactive or suspended.");

    public static readonly Error UserAlreadyMember =
        Error.Conflict("Workspace.UserAlreadyMember", "The specified user is already a member of this workspace.");

    public static readonly Error MemberNotFound =
        Error.NotFound("Workspace.MemberNotFound", "The specified member does not belong to this workspace.");

    public static readonly Error CannotRemoveOwner =
        Error.Validation("Workspace.CannotRemoveOwner", "The primary workspace owner cannot be removed.");

    public static readonly Error FreePlanWorkspaceLimitReached =
        Error.Forbidden("Workspace.FreePlanWorkspaceLimitReached", "Free plan accounts are limited to one workspace. Upgrade to Pro or Enterprise to create additional workspaces.");
}

/// <summary>
/// Domain errors related to multi-tenant resolution and context.
/// </summary>
public static class TenantErrors
{
    public static readonly Error NotResolved =
        Error.Unauthorized("Tenant.NotResolved", "Could not resolve a valid tenant context from the incoming request (subdomain, header, or claim).");

    public static readonly Error Mismatch =
        Error.Forbidden("Tenant.Mismatch", "The authenticated user does not have permission to access the resolved tenant workspace.");
}

/// <summary>
/// Domain errors related to SaaS subscription and quota management.
/// </summary>
public static class SubscriptionErrors
{
    public static readonly Error NotFound =
        Error.NotFound("Subscription.NotFound", "Subscription details for this workspace were not found.");

    public static readonly Error QuotaExceeded =
        Error.Forbidden("Subscription.QuotaExceeded", "Your monthly AI query limit for your current subscription plan has been reached.");

    public static readonly Error DataSourceLimitReached =
        Error.Forbidden("Subscription.DataSourceLimitReached", "The maximum number of connected data sources for your current subscription plan has been reached.");
}

/// <summary>
/// Domain errors related to payment processing and gateway communication.
/// </summary>
public static class PaymentErrors
{
    public static readonly Error InitializationFailed =
        Error.Failure("Payment.InitializationFailed", "Failed to initialize payment transaction with the payment gateway.");

    public static readonly Error VerificationFailed =
        Error.Failure("Payment.VerificationFailed", "Payment verification failed or transaction was not successful.");

    public static readonly Error InvalidReference =
        Error.Validation("Payment.InvalidReference", "A valid payment transaction reference is required.");

    public static readonly Error WorkspaceNotFound =
        Error.NotFound("Payment.WorkspaceNotFound", "Workspace for subscription upgrade was not found.");
}

