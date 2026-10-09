using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Application.Features.Subscriptions.Dtos;
using IntelliQueryAI.Domain.Entities.Subscriptions;
using IntelliQueryAI.Domain.Errors;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.SharedKernel.Results;

namespace IntelliQueryAI.Application.Features.Subscriptions.Queries;

/// <summary>
/// Query to retrieve subscription tier, quota limits, and billing status for a workspace.
/// </summary>
public sealed record GetSubscriptionDetailsQuery(Guid WorkspaceId) : IQuery<Result<SubscriptionDto>>;

public sealed class GetSubscriptionDetailsQueryHandler : IQueryHandler<GetSubscriptionDetailsQuery, Result<SubscriptionDto>>
{
    private readonly ISubscriptionStore _subscriptionStore;
    private readonly IWorkspaceStore _workspaceStore;
    private readonly IUnitOfWork _unitOfWork;

    public GetSubscriptionDetailsQueryHandler(
        ISubscriptionStore subscriptionStore,
        IWorkspaceStore workspaceStore,
        IUnitOfWork unitOfWork)
    {
        _subscriptionStore = subscriptionStore;
        _workspaceStore = workspaceStore;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<SubscriptionDto>> HandleAsync(GetSubscriptionDetailsQuery query, CancellationToken cancellationToken = default)
    {
        var subscription = await _subscriptionStore.GetByWorkspaceIdAsync(query.WorkspaceId, cancellationToken);
        if (subscription is null)
        {
            var workspace = await _workspaceStore.GetByIdAsync(query.WorkspaceId, cancellationToken);
            if (workspace is null)
            {
                return Result.Failure<SubscriptionDto>(SubscriptionErrors.NotFound);
            }

            // Self-heal: ensure default subscription exists for this valid workspace
            subscription = Subscription.CreateDefault(workspace.Id, workspace.SubscriptionPlan);
            await _subscriptionStore.AddAsync(subscription, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return new SubscriptionDto(
            subscription.WorkspaceId,
            subscription.Plan.ToString(),
            subscription.Status.ToString(),
            subscription.MaxDataSources,
            subscription.MaxQueriesPerMonth,
            subscription.CurrentQueriesCount,
            subscription.CurrentPeriodStartUtc,
            subscription.CurrentPeriodEndUtc);
    }
}
