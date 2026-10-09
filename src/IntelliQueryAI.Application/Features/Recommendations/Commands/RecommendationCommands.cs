using IntelliQueryAI.AI.Coordinator;
using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Application.Features.Recommendations.Dtos;
using IntelliQueryAI.Domain.Entities.Recommendations;
using IntelliQueryAI.Domain.Errors;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.SharedKernel.Results;

namespace IntelliQueryAI.Application.Features.Recommendations.Commands;

public sealed record GenerateRecommendationsCommand(
    Guid WorkspaceId,
    Guid? UserId) : ICommand<Result<IReadOnlyList<RecommendationDto>>>;

public sealed class GenerateRecommendationsCommandHandler
    : ICommandHandler<GenerateRecommendationsCommand, Result<IReadOnlyList<RecommendationDto>>>
{
    private readonly IRecommendationCoordinator _coordinator;

    public GenerateRecommendationsCommandHandler(IRecommendationCoordinator coordinator)
    {
        _coordinator = coordinator;
    }

    public async Task<Result<IReadOnlyList<RecommendationDto>>> HandleAsync(
        GenerateRecommendationsCommand command,
        CancellationToken cancellationToken = default)
    {
        var entities = await _coordinator.GenerateRecommendationsAsync(
            command.WorkspaceId,
            command.UserId,
            cancellationToken);

        var dtos = entities.Select(RecommendationMapping.ToDto).ToList();
        return Result.Success<IReadOnlyList<RecommendationDto>>(dtos);
    }
}

public sealed record CreateRecommendationCommand(
    Guid WorkspaceId,
    Guid? UserId,
    CreateRecommendationRequestDto Request) : ICommand<Result<RecommendationDto>>;

public sealed class CreateRecommendationCommandHandler
    : ICommandHandler<CreateRecommendationCommand, Result<RecommendationDto>>
{
    private readonly IRecommendationStore _store;

    public CreateRecommendationCommandHandler(IRecommendationStore store)
    {
        _store = store;
    }

    public async Task<Result<RecommendationDto>> HandleAsync(
        CreateRecommendationCommand command,
        CancellationToken cancellationToken = default)
    {
        var req = command.Request;

        if (string.IsNullOrWhiteSpace(req.Title))
        {
            return Result.Failure<RecommendationDto>(
                Error.Validation("Recommendation.InvalidTitle", "Recommendation title is required."));
        }

        if (string.IsNullOrWhiteSpace(req.Rationale))
        {
            return Result.Failure<RecommendationDto>(
                Error.Validation("Recommendation.InvalidRationale", "Recommendation rationale is required."));
        }

        if (!Enum.TryParse<RecommendationPriority>(req.Priority, true, out var priority))
        {
            priority = RecommendationPriority.Medium;
        }

        var evidence = req.Evidence?.Select(e => new RecommendationEvidenceItem(
            e.Title,
            e.Description,
            e.MetricName,
            e.MetricValue,
            e.BaselineValue,
            e.EvidenceType,
            e.LinkUrl)).ToList();

        var actionSteps = req.ActionSteps?.Select(a => new RecommendationActionStep(
            a.StepNumber,
            a.Action,
            a.ExpectedImpact,
            a.AutomatedActionPossible)).ToList();

        var recommendation = new Recommendation(
            workspaceId: command.WorkspaceId,
            createdByUserId: command.UserId,
            title: req.Title.Trim(),
            rationale: req.Rationale.Trim(),
            category: req.Category?.Trim() ?? "Strategic",
            priority: priority,
            targetMetric: req.TargetMetric?.Trim(),
            impactMagnitude: req.ImpactMagnitude?.Trim(),
            estimatedEffort: req.EstimatedEffort?.Trim(),
            evidence: evidence,
            actionSteps: actionSteps,
            relatedInvestigationId: req.RelatedInvestigationId,
            relatedAlertId: req.RelatedAlertId,
            relatedMonitorId: req.RelatedMonitorId,
            relatedScenarioId: req.RelatedScenarioId);

        await _store.AddAsync(recommendation, cancellationToken);
        return Result.Success(RecommendationMapping.ToDto(recommendation));
    }
}

public sealed record ApplyRecommendationCommand(
    Guid WorkspaceId,
    Guid UserId,
    Guid RecommendationId) : ICommand<Result<RecommendationDto>>;

public sealed class ApplyRecommendationCommandHandler
    : ICommandHandler<ApplyRecommendationCommand, Result<RecommendationDto>>
{
    private readonly IRecommendationStore _store;

    public ApplyRecommendationCommandHandler(IRecommendationStore store)
    {
        _store = store;
    }

    public async Task<Result<RecommendationDto>> HandleAsync(
        ApplyRecommendationCommand command,
        CancellationToken cancellationToken = default)
    {
        var recommendation = await _store.GetByIdAsync(command.RecommendationId, command.WorkspaceId, cancellationToken);
        if (recommendation == null)
        {
            return Result.Failure<RecommendationDto>(AiErrors.RecommendationNotFound);
        }

        if (recommendation.WorkspaceId != command.WorkspaceId)
        {
            return Result.Failure<RecommendationDto>(AiErrors.RecommendationTenantMismatch);
        }

        if (recommendation.Status == RecommendationStatus.Applied)
        {
            return Result.Failure<RecommendationDto>(AiErrors.RecommendationAlreadyApplied);
        }

        recommendation.MarkApplied(command.UserId);
        await _store.UpdateAsync(recommendation, cancellationToken);

        return Result.Success(RecommendationMapping.ToDto(recommendation));
    }
}

public sealed record DismissRecommendationCommand(
    Guid WorkspaceId,
    Guid UserId,
    Guid RecommendationId,
    string? Reason = null) : ICommand<Result<RecommendationDto>>;

public sealed class DismissRecommendationCommandHandler
    : ICommandHandler<DismissRecommendationCommand, Result<RecommendationDto>>
{
    private readonly IRecommendationStore _store;

    public DismissRecommendationCommandHandler(IRecommendationStore store)
    {
        _store = store;
    }

    public async Task<Result<RecommendationDto>> HandleAsync(
        DismissRecommendationCommand command,
        CancellationToken cancellationToken = default)
    {
        var recommendation = await _store.GetByIdAsync(command.RecommendationId, command.WorkspaceId, cancellationToken);
        if (recommendation == null)
        {
            return Result.Failure<RecommendationDto>(AiErrors.RecommendationNotFound);
        }

        if (recommendation.WorkspaceId != command.WorkspaceId)
        {
            return Result.Failure<RecommendationDto>(AiErrors.RecommendationTenantMismatch);
        }

        if (recommendation.Status == RecommendationStatus.Dismissed)
        {
            return Result.Failure<RecommendationDto>(AiErrors.RecommendationAlreadyDismissed);
        }

        recommendation.Dismiss(command.UserId, command.Reason?.Trim());
        await _store.UpdateAsync(recommendation, cancellationToken);

        return Result.Success(RecommendationMapping.ToDto(recommendation));
    }
}

public sealed record ReactivateRecommendationCommand(
    Guid WorkspaceId,
    Guid UserId,
    Guid RecommendationId) : ICommand<Result<RecommendationDto>>;

public sealed class ReactivateRecommendationCommandHandler
    : ICommandHandler<ReactivateRecommendationCommand, Result<RecommendationDto>>
{
    private readonly IRecommendationStore _store;

    public ReactivateRecommendationCommandHandler(IRecommendationStore store)
    {
        _store = store;
    }

    public async Task<Result<RecommendationDto>> HandleAsync(
        ReactivateRecommendationCommand command,
        CancellationToken cancellationToken = default)
    {
        var recommendation = await _store.GetByIdAsync(command.RecommendationId, command.WorkspaceId, cancellationToken);
        if (recommendation == null)
        {
            return Result.Failure<RecommendationDto>(AiErrors.RecommendationNotFound);
        }

        if (recommendation.WorkspaceId != command.WorkspaceId)
        {
            return Result.Failure<RecommendationDto>(AiErrors.RecommendationTenantMismatch);
        }

        recommendation.Reactivate(command.UserId);
        await _store.UpdateAsync(recommendation, cancellationToken);

        return Result.Success(RecommendationMapping.ToDto(recommendation));
    }
}

public sealed record DeleteRecommendationCommand(
    Guid WorkspaceId,
    Guid UserId,
    Guid RecommendationId) : ICommand<Result<bool>>;

public sealed class DeleteRecommendationCommandHandler
    : ICommandHandler<DeleteRecommendationCommand, Result<bool>>
{
    private readonly IRecommendationStore _store;

    public DeleteRecommendationCommandHandler(IRecommendationStore store)
    {
        _store = store;
    }

    public async Task<Result<bool>> HandleAsync(
        DeleteRecommendationCommand command,
        CancellationToken cancellationToken = default)
    {
        var recommendation = await _store.GetByIdAsync(command.RecommendationId, command.WorkspaceId, cancellationToken);
        if (recommendation == null)
        {
            return Result.Failure<bool>(AiErrors.RecommendationNotFound);
        }

        if (recommendation.WorkspaceId != command.WorkspaceId)
        {
            return Result.Failure<bool>(AiErrors.RecommendationTenantMismatch);
        }

        await _store.DeleteAsync(recommendation, cancellationToken);
        return Result.Success(true);
    }
}

internal static class RecommendationMapping
{
    public static RecommendationDto ToDto(Recommendation r)
    {
        var evidence = r.GetEvidence().Select(e => new RecommendationEvidenceItemDto(
            e.Title,
            e.Description,
            e.MetricName,
            e.MetricValue,
            e.BaselineValue,
            e.EvidenceType,
            e.LinkUrl)).ToList();

        var actionSteps = r.GetActionSteps().Select(a => new RecommendationActionStepDto(
            a.StepNumber,
            a.Action,
            a.ExpectedImpact,
            a.AutomatedActionPossible)).ToList();

        return new RecommendationDto(
            r.Id,
            r.WorkspaceId,
            r.CreatedByUserId,
            r.Title,
            r.Rationale,
            r.Category,
            r.Priority.ToString(),
            r.Status.ToString(),
            r.TargetMetric,
            r.ImpactMagnitude,
            r.EstimatedEffort,
            evidence,
            actionSteps,
            r.RelatedInvestigationId,
            r.RelatedAlertId,
            r.RelatedMonitorId,
            r.RelatedScenarioId,
            r.AppliedAtUtc,
            r.DismissedAtUtc,
            r.DismissalReason,
            r.CreatedAtUtc,
            r.UpdatedAtUtc);
    }
}
