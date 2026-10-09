using IntelliQueryAI.Domain.Enums;
using IntelliQueryAI.SharedKernel.Primitives;

namespace IntelliQueryAI.Domain.Entities.Subscriptions;

/// <summary>
/// Aggregate root managing SaaS subscription plan, status, and query quotas for a workspace.
/// </summary>
public sealed class Subscription : AggregateRoot
{
    /// <summary>
    /// Gets the associated tenant workspace identifier.
    /// </summary>
    public Guid WorkspaceId { get; private set; }

    /// <summary>
    /// Gets the current subscription plan.
    /// </summary>
    public SubscriptionPlan Plan { get; private set; }

    /// <summary>
    /// Gets the subscription status.
    /// </summary>
    public SubscriptionStatus Status { get; private set; }

    /// <summary>
    /// Gets the maximum number of simultaneous data sources allowed.
    /// </summary>
    public int MaxDataSources { get; private set; }

    /// <summary>
    /// Gets the maximum monthly AI query quota.
    /// </summary>
    public int MaxQueriesPerMonth { get; private set; }

    /// <summary>
    /// Gets the number of queries executed during the current billing period.
    /// </summary>
    public int CurrentQueriesCount { get; private set; }

    /// <summary>
    /// Gets the start date of the current billing period in UTC.
    /// </summary>
    public DateTime CurrentPeriodStartUtc { get; private set; }

    /// <summary>
    /// Gets the end date of the current billing period in UTC.
    /// </summary>
    public DateTime CurrentPeriodEndUtc { get; private set; }

    private Subscription(
        Guid workspaceId,
        SubscriptionPlan plan,
        SubscriptionStatus status,
        int maxDataSources,
        int maxQueriesPerMonth,
        DateTime periodStart,
        DateTime periodEnd)
        : base(Guid.NewGuid())
    {
        WorkspaceId = workspaceId;
        Plan = plan;
        Status = status;
        MaxDataSources = maxDataSources;
        MaxQueriesPerMonth = maxQueriesPerMonth;
        CurrentQueriesCount = 0;
        CurrentPeriodStartUtc = periodStart;
        CurrentPeriodEndUtc = periodEnd;
    }

    /// <summary>
    /// Required for EF Core.
    /// </summary>
    private Subscription()
    {
    }

    /// <summary>
    /// Creates a default subscription tier for a new workspace.
    /// </summary>
    public static Subscription CreateDefault(Guid workspaceId, SubscriptionPlan plan = SubscriptionPlan.Free)
    {
        var now = DateTime.UtcNow;
        var endOfBillingPeriod = now.AddDays(30);

        var (maxDataSources, maxQueries) = plan switch
        {
            SubscriptionPlan.Free => (1, 100),
            SubscriptionPlan.Pro => (5, 5_000),
            SubscriptionPlan.Enterprise => (100, 100_000),
            _ => (1, 100)
        };

        return new Subscription(
            workspaceId,
            plan,
            SubscriptionStatus.Active,
            maxDataSources,
            maxQueries,
            now,
            endOfBillingPeriod);
    }

    /// <summary>
    /// Determines whether an AI query can be executed based on monthly quota.
    /// </summary>
    public bool CanExecuteQuery()
    {
        return Status == SubscriptionStatus.Active && CurrentQueriesCount < MaxQueriesPerMonth;
    }

    /// <summary>
    /// Increments the executed query counter.
    /// </summary>
    public void IncrementQueryUsage()
    {
        CurrentQueriesCount++;
    }

    /// <summary>
    /// Upgrades or downgrades the subscription plan.
    /// </summary>
    public void ChangePlan(SubscriptionPlan newPlan)
    {
        Plan = newPlan;
        var (maxDataSources, maxQueries) = newPlan switch
        {
            SubscriptionPlan.Free => (1, 100),
            SubscriptionPlan.Pro => (5, 5_000),
            SubscriptionPlan.Enterprise => (100, 100_000),
            _ => (1, 100)
        };

        MaxDataSources = maxDataSources;
        MaxQueriesPerMonth = maxQueries;
    }

    /// <summary>
    /// Resets the billing cycle quota counters.
    /// </summary>
    public void ResetBillingPeriod(DateTime newPeriodStart, DateTime newPeriodEnd)
    {
        CurrentPeriodStartUtc = newPeriodStart;
        CurrentPeriodEndUtc = newPeriodEnd;
        CurrentQueriesCount = 0;
    }
}
