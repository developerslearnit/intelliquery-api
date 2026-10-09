using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Application.Features.Recommendations.Commands;
using IntelliQueryAI.Application.Features.Recommendations.Dtos;
using IntelliQueryAI.Domain.Entities.Recommendations;
using IntelliQueryAI.Domain.Errors;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.SharedKernel.Results;

namespace IntelliQueryAI.Application.Features.Recommendations.Queries;

public sealed record GetRecommendationsQuery(
    Guid WorkspaceId,
    string? Status = null,
    string? Priority = null,
    int Limit = 50) : IQuery<Result<IReadOnlyList<RecommendationDto>>>;

public sealed class GetRecommendationsQueryHandler
    : IQueryHandler<GetRecommendationsQuery, Result<IReadOnlyList<RecommendationDto>>>
{
    private readonly IRecommendationStore _store;

    public GetRecommendationsQueryHandler(IRecommendationStore store)
    {
        _store = store;
    }

    public async Task<Result<IReadOnlyList<RecommendationDto>>> HandleAsync(
        GetRecommendationsQuery query,
        CancellationToken cancellationToken = default)
    {
        RecommendationStatus? statusEnum = null;
        if (!string.IsNullOrWhiteSpace(query.Status) &&
            Enum.TryParse<RecommendationStatus>(query.Status, true, out var parsedStatus))
        {
            statusEnum = parsedStatus;
        }

        RecommendationPriority? priorityEnum = null;
        if (!string.IsNullOrWhiteSpace(query.Priority) &&
            Enum.TryParse<RecommendationPriority>(query.Priority, true, out var parsedPriority))
        {
            priorityEnum = parsedPriority;
        }

        var entities = await _store.GetByWorkspaceAsync(
            query.WorkspaceId,
            statusEnum,
            priorityEnum,
            query.Limit,
            cancellationToken);

        var dtos = entities.Select(RecommendationMapping.ToDto).ToList();
        return Result.Success<IReadOnlyList<RecommendationDto>>(dtos);
    }
}

public sealed record GetRecommendationByIdQuery(
    Guid WorkspaceId,
    Guid RecommendationId) : IQuery<Result<RecommendationDto>>;

public sealed class GetRecommendationByIdQueryHandler
    : IQueryHandler<GetRecommendationByIdQuery, Result<RecommendationDto>>
{
    private readonly IRecommendationStore _store;

    public GetRecommendationByIdQueryHandler(IRecommendationStore store)
    {
        _store = store;
    }

    public async Task<Result<RecommendationDto>> HandleAsync(
        GetRecommendationByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        var entity = await _store.GetByIdAsync(query.RecommendationId, query.WorkspaceId, cancellationToken);
        if (entity == null)
        {
            return Result.Failure<RecommendationDto>(AiErrors.RecommendationNotFound);
        }

        if (entity.WorkspaceId != query.WorkspaceId)
        {
            return Result.Failure<RecommendationDto>(AiErrors.RecommendationTenantMismatch);
        }

        return Result.Success(RecommendationMapping.ToDto(entity));
    }
}

public sealed record GetRecommendationSummaryQuery(
    Guid WorkspaceId) : IQuery<Result<RecommendationSummaryDto>>;

public sealed class GetRecommendationSummaryQueryHandler
    : IQueryHandler<GetRecommendationSummaryQuery, Result<RecommendationSummaryDto>>
{
    private readonly IRecommendationStore _store;

    public GetRecommendationSummaryQueryHandler(IRecommendationStore store)
    {
        _store = store;
    }

    public async Task<Result<RecommendationSummaryDto>> HandleAsync(
        GetRecommendationSummaryQuery query,
        CancellationToken cancellationToken = default)
    {
        var active = await _store.CountByStatusAsync(query.WorkspaceId, RecommendationStatus.Active, cancellationToken);
        var critical = await _store.CountCriticalActiveAsync(query.WorkspaceId, cancellationToken);
        var applied = await _store.CountByStatusAsync(query.WorkspaceId, RecommendationStatus.Applied, cancellationToken);
        var dismissed = await _store.CountByStatusAsync(query.WorkspaceId, RecommendationStatus.Dismissed, cancellationToken);

        var summary = new RecommendationSummaryDto(
            TotalActive: active,
            TotalCriticalActive: critical,
            TotalApplied: applied,
            TotalDismissed: dismissed);

        return Result.Success(summary);
    }
}
