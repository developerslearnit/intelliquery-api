namespace IntelliQueryAI.Application.Features.Subscriptions.Dtos;

public sealed record SubscriptionDto(
    Guid WorkspaceId,
    string Plan,
    string Status,
    int MaxDataSources,
    int MaxQueriesPerMonth,
    int CurrentQueriesCount,
    DateTime PeriodStartUtc,
    DateTime PeriodEndUtc);
