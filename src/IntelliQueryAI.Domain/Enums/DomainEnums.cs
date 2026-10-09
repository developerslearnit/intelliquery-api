namespace IntelliQueryAI.Domain.Enums;

/// <summary>
/// Available SaaS subscription plans for a workspace.
/// </summary>
public enum SubscriptionPlan
{
    /// <summary>
    /// Free tier for individual exploration and small databases.
    /// </summary>
    Free = 0,

    /// <summary>
    /// Professional tier with expanded limits and data sources.
    /// </summary>
    Pro = 1,

    /// <summary>
    /// Enterprise tier with full multi-agent orchestration, unlimited data sources, and dedicated throughput.
    /// </summary>
    Enterprise = 2
}

/// <summary>
/// Status of a workspace subscription billing cycle.
/// </summary>
public enum SubscriptionStatus
{
    /// <summary>
    /// Subscription is fully active and in good standing.
    /// </summary>
    Active = 0,

    /// <summary>
    /// Subscription payment is past due.
    /// </summary>
    PastDue = 1,

    /// <summary>
    /// Subscription has been canceled.
    /// </summary>
    Canceled = 2,

    /// <summary>
    /// Free trial period active.
    /// </summary>
    Trialing = 3
}

/// <summary>
/// Role assigned to a user within a specific tenant workspace.
/// </summary>
public enum WorkspaceRole
{
    /// <summary>
    /// Workspace creator with full administrative and billing control.
    /// </summary>
    Owner = 0,

    /// <summary>
    /// Administrator capable of adding members, managing data sources, and viewing audit logs.
    /// </summary>
    Admin = 1,

    /// <summary>
    /// Regular member permitted to query data and view dashboards.
    /// </summary>
    Member = 2,

    /// <summary>
    /// Read-only viewer permitted to consume generated reports and saved dashboards.
    /// </summary>
    Viewer = 3
}

/// <summary>
/// Platform-wide system role for platform administrators.
/// </summary>
public enum SystemRole
{
    /// <summary>
    /// Global system operator with cross-tenant administrative privileges.
    /// </summary>
    SystemAdmin = 0,

    /// <summary>
    /// Standard platform user.
    /// </summary>
    User = 1
}
