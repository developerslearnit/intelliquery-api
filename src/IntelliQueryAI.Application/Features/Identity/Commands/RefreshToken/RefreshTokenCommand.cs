using FluentValidation;
using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Application.Features.Identity.Dtos;
using IntelliQueryAI.Application.Features.Workspaces.Dtos;
using IntelliQueryAI.Domain.Entities.Audit;
using IntelliQueryAI.Domain.Entities.Users;
using IntelliQueryAI.Domain.Enums;
using IntelliQueryAI.Domain.Errors;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.Domain.Services;
using IntelliQueryAI.SharedKernel.Results;

namespace IntelliQueryAI.Application.Features.Identity.Commands.RefreshToken;

/// <summary>
/// Command to rotate an existing refresh token and obtain a fresh JWT access token.
/// </summary>
public sealed record RefreshTokenCommand(
    string RefreshToken,
    Guid? CurrentWorkspaceId = null) : ICommand<Result<AuthResponseDto>>;

public sealed class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty().WithMessage("Refresh token is required.");
    }
}

public sealed class RefreshTokenCommandHandler : ICommandHandler<RefreshTokenCommand, Result<AuthResponseDto>>
{
    private readonly IRefreshTokenStore _refreshTokenStore;
    private readonly IUserStore _userStore;
    private readonly IWorkspaceStore _workspaceStore;
    private readonly IAuditLogStore _auditLogStore;
    private readonly IJwtTokenGenerator _tokenGenerator;
    private readonly IUnitOfWork _unitOfWork;

    public RefreshTokenCommandHandler(
        IRefreshTokenStore refreshTokenStore,
        IUserStore userStore,
        IWorkspaceStore workspaceStore,
        IAuditLogStore auditLogStore,
        IJwtTokenGenerator tokenGenerator,
        IUnitOfWork unitOfWork)
    {
        _refreshTokenStore = refreshTokenStore;
        _userStore = userStore;
        _workspaceStore = workspaceStore;
        _auditLogStore = auditLogStore;
        _tokenGenerator = tokenGenerator;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<AuthResponseDto>> HandleAsync(RefreshTokenCommand command, CancellationToken cancellationToken = default)
    {
        var existingToken = await _refreshTokenStore.GetByTokenAsync(command.RefreshToken, cancellationToken);
        if (existingToken is null || !existingToken.IsActive)
        {
            return Result.Failure<AuthResponseDto>(IdentityErrors.InvalidRefreshToken);
        }

        var user = await _userStore.GetByIdAsync(existingToken.UserId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return Result.Failure<AuthResponseDto>(IdentityErrors.UserInactive);
        }

        // Rotate token
        string newRefreshTokenStr = _tokenGenerator.GenerateRefreshToken();
        existingToken.Revoke(newRefreshTokenStr);

        var newRefreshToken = IntelliQueryAI.Domain.Entities.Users.RefreshToken.Create(
            user.Id,
            newRefreshTokenStr,
            DateTime.UtcNow.AddDays(7));

        await _refreshTokenStore.AddAsync(newRefreshToken, cancellationToken);

        // Resolve workspace
        var workspaces = await _workspaceStore.GetUserWorkspacesAsync(user.Id, cancellationToken);
        var activeWorkspace = command.CurrentWorkspaceId.HasValue
            ? workspaces.FirstOrDefault(w => w.Id == command.CurrentWorkspaceId.Value)
            : workspaces.FirstOrDefault();

        string? workspaceRole = null;
        if (activeWorkspace is not null)
        {
            var member = activeWorkspace.Members.FirstOrDefault(m => m.UserId == user.Id);
            workspaceRole = member?.Role.ToString() ?? WorkspaceRole.Member.ToString();
        }

        string accessToken = _tokenGenerator.GenerateAccessToken(user, activeWorkspace, workspaceRole);

        await _auditLogStore.AddAsync(AuditLog.Create(
            activeWorkspace?.Id,
            user.Id,
            "User.RefreshToken",
            "RefreshToken",
            newRefreshToken.Id.ToString(),
            $"Refreshed token for user {user.Email}."),
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var userDto = new UserDto(
            user.Id,
            user.Email.Value,
            user.FirstName,
            user.LastName,
            user.SystemRole.ToString(),
            user.IsEmailVerified);

        WorkspaceDto? workspaceDto = activeWorkspace is not null
            ? new WorkspaceDto(
                activeWorkspace.Id,
                activeWorkspace.Name,
                activeWorkspace.Slug.Value,
                activeWorkspace.SubscriptionPlan.ToString(),
                activeWorkspace.IsActive,
                activeWorkspace.CreatedAtUtc)
            : null;

        return new AuthResponseDto(
            accessToken,
            newRefreshTokenStr,
            DateTime.UtcNow.AddMinutes(60),
            userDto,
            workspaceDto);
    }
}
