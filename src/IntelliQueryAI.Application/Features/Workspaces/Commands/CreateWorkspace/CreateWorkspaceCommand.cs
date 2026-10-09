using FluentValidation;
using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Application.Features.Workspaces.Dtos;
using IntelliQueryAI.Domain.Entities.Audit;
using IntelliQueryAI.Domain.Entities.Subscriptions;
using IntelliQueryAI.Domain.Entities.Workspaces;
using IntelliQueryAI.Domain.Enums;
using IntelliQueryAI.Domain.Errors;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.Domain.ValueObjects;
using IntelliQueryAI.SharedKernel.Results;

namespace IntelliQueryAI.Application.Features.Workspaces.Commands.CreateWorkspace;

/// <summary>
/// Command to create a new tenant workspace for an authenticated owner.
/// </summary>
public sealed record CreateWorkspaceCommand(
    Guid OwnerUserId,
    string Name,
    string Slug,
    SubscriptionPlan SubscriptionPlan = SubscriptionPlan.Free) : ICommand<Result<WorkspaceDto>>;

public sealed class CreateWorkspaceCommandValidator : AbstractValidator<CreateWorkspaceCommand>
{
    public CreateWorkspaceCommandValidator()
    {
        RuleFor(x => x.OwnerUserId).NotEmpty().WithMessage("Owner user identifier is required.");
        RuleFor(x => x.Name).NotEmpty().WithMessage("Workspace name is required.");
        RuleFor(x => x.Slug)
            .NotEmpty().WithMessage("Workspace slug is required.")
            .MinimumLength(3).WithMessage("Workspace slug must be at least 3 characters.")
            .MaximumLength(63).WithMessage("Workspace slug cannot exceed 63 characters.");
    }
}

public sealed class CreateWorkspaceCommandHandler : ICommandHandler<CreateWorkspaceCommand, Result<WorkspaceDto>>
{
    private readonly IWorkspaceStore _workspaceStore;
    private readonly ISubscriptionStore _subscriptionStore;
    private readonly IAuditLogStore _auditLogStore;
    private readonly IUnitOfWork _unitOfWork;

    public CreateWorkspaceCommandHandler(
        IWorkspaceStore workspaceStore,
        ISubscriptionStore subscriptionStore,
        IAuditLogStore auditLogStore,
        IUnitOfWork unitOfWork)
    {
        _workspaceStore = workspaceStore;
        _subscriptionStore = subscriptionStore;
        _auditLogStore = auditLogStore;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<WorkspaceDto>> HandleAsync(CreateWorkspaceCommand command, CancellationToken cancellationToken = default)
    {
        var slugResult = TenantSlug.Create(command.Slug);
        if (slugResult.IsFailure)
        {
            return Result.Failure<WorkspaceDto>(slugResult.Error);
        }

        if (await _workspaceStore.SlugExistsAsync(command.Slug, cancellationToken))
        {
            return Result.Failure<WorkspaceDto>(WorkspaceErrors.SlugAlreadyExists);
        }

        // Enforce 1-workspace limit for Free tier accounts
        var existingWorkspaces = await _workspaceStore.GetUserWorkspacesAsync(command.OwnerUserId, cancellationToken);
        bool hasPaidPlan = existingWorkspaces.Any(w => w.SubscriptionPlan is SubscriptionPlan.Pro or SubscriptionPlan.Enterprise);
        if (!hasPaidPlan && existingWorkspaces.Count >= 1 && command.SubscriptionPlan == SubscriptionPlan.Free)
        {
            return Result.Failure<WorkspaceDto>(WorkspaceErrors.FreePlanWorkspaceLimitReached);
        }

        var workspaceResult = Workspace.Create(command.Name, slugResult.Value, command.SubscriptionPlan, command.OwnerUserId);
        if (workspaceResult.IsFailure)
        {
            return Result.Failure<WorkspaceDto>(workspaceResult.Error);
        }

        var workspace = workspaceResult.Value;
        await _workspaceStore.AddAsync(workspace, cancellationToken);

        var subscription = Subscription.CreateDefault(workspace.Id, command.SubscriptionPlan);
        await _subscriptionStore.AddAsync(subscription, cancellationToken);

        await _auditLogStore.AddAsync(AuditLog.Create(
            workspace.Id,
            command.OwnerUserId,
            "Workspace.Create",
            "Workspace",
            workspace.Id.ToString(),
            $"Created workspace '{workspace.Name}' with slug '{workspace.Slug}'."),
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new WorkspaceDto(
            workspace.Id,
            workspace.Name,
            workspace.Slug.Value,
            workspace.SubscriptionPlan.ToString(),
            workspace.IsActive,
            workspace.CreatedAtUtc);
    }
}
