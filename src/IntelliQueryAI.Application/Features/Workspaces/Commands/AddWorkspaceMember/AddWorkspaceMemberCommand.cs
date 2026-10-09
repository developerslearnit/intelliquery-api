using FluentValidation;
using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Domain.Entities.Audit;
using IntelliQueryAI.Domain.Enums;
using IntelliQueryAI.Domain.Errors;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.SharedKernel.Results;

namespace IntelliQueryAI.Application.Features.Workspaces.Commands.AddWorkspaceMember;

/// <summary>
/// Command to add an existing registered user to a workspace with a specified role.
/// </summary>
public sealed record AddWorkspaceMemberCommand(
    Guid WorkspaceId,
    string MemberEmail,
    WorkspaceRole Role,
    Guid RequestedByUserId) : ICommand<Result>;

public sealed class AddWorkspaceMemberCommandValidator : AbstractValidator<AddWorkspaceMemberCommand>
{
    public AddWorkspaceMemberCommandValidator()
    {
        RuleFor(x => x.WorkspaceId).NotEmpty().WithMessage("Workspace identifier is required.");
        RuleFor(x => x.MemberEmail).NotEmpty().EmailAddress().WithMessage("Valid member email is required.");
        RuleFor(x => x.RequestedByUserId).NotEmpty().WithMessage("Requesting user identifier is required.");
    }
}

public sealed class AddWorkspaceMemberCommandHandler : ICommandHandler<AddWorkspaceMemberCommand, Result>
{
    private readonly IWorkspaceStore _workspaceStore;
    private readonly IUserStore _userStore;
    private readonly IAuditLogStore _auditLogStore;
    private readonly IUnitOfWork _unitOfWork;

    public AddWorkspaceMemberCommandHandler(
        IWorkspaceStore workspaceStore,
        IUserStore userStore,
        IAuditLogStore auditLogStore,
        IUnitOfWork unitOfWork)
    {
        _workspaceStore = workspaceStore;
        _userStore = userStore;
        _auditLogStore = auditLogStore;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(AddWorkspaceMemberCommand command, CancellationToken cancellationToken = default)
    {
        var workspace = await _workspaceStore.GetByIdAsync(command.WorkspaceId, cancellationToken);
        if (workspace is null)
        {
            return Result.Failure(WorkspaceErrors.NotFound);
        }

        var requestingMember = workspace.Members.FirstOrDefault(m => m.UserId == command.RequestedByUserId);
        if (requestingMember is null || (requestingMember.Role != WorkspaceRole.Owner && requestingMember.Role != WorkspaceRole.Admin))
        {
            return Result.Failure(TenantErrors.Mismatch);
        }

        var memberUser = await _userStore.GetByEmailAsync(command.MemberEmail, cancellationToken);
        if (memberUser is null)
        {
            return Result.Failure(IdentityErrors.UserNotFound);
        }

        var addResult = workspace.AddMember(memberUser.Id, command.Role);
        if (addResult.IsFailure)
        {
            return Result.Failure(addResult.Error);
        }

        await _auditLogStore.AddAsync(AuditLog.Create(
            workspace.Id,
            command.RequestedByUserId,
            "Workspace.AddMember",
            "WorkspaceMember",
            memberUser.Id.ToString(),
            $"Added user '{memberUser.Email}' as {command.Role} to workspace '{workspace.Name}'."),
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
