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
using DomainRefreshToken = IntelliQueryAI.Domain.Entities.Users.RefreshToken;

namespace IntelliQueryAI.Application.Features.Identity.Commands.LoginUser;

/// <summary>
/// Command to authenticate an existing user via credentials and optional tenant slug.
/// </summary>
public sealed record LoginUserCommand(
    string Email,
    string Password,
    string? WorkspaceSlug = null) : ICommand<Result<AuthResponseDto>>;

public sealed class LoginUserCommandValidator : AbstractValidator<LoginUserCommand>
{
    public LoginUserCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.");
    }
}

public sealed class LoginUserCommandHandler : ICommandHandler<LoginUserCommand, Result<AuthResponseDto>>
{
    private readonly IUserStore _userStore;
    private readonly IWorkspaceStore _workspaceStore;
    private readonly IRefreshTokenStore _refreshTokenStore;
    private readonly IAuditLogStore _auditLogStore;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _tokenGenerator;
    private readonly IUnitOfWork _unitOfWork;

    public LoginUserCommandHandler(
        IUserStore userStore,
        IWorkspaceStore workspaceStore,
        IRefreshTokenStore refreshTokenStore,
        IAuditLogStore auditLogStore,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator tokenGenerator,
        IUnitOfWork unitOfWork)
    {
        _userStore = userStore;
        _workspaceStore = workspaceStore;
        _refreshTokenStore = refreshTokenStore;
        _auditLogStore = auditLogStore;
        _passwordHasher = passwordHasher;
        _tokenGenerator = tokenGenerator;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<AuthResponseDto>> HandleAsync(LoginUserCommand command, CancellationToken cancellationToken = default)
    {
        var user = await _userStore.GetByEmailAsync(command.Email, cancellationToken);
        if (user is null)
        {
            return Result.Failure<AuthResponseDto>(IdentityErrors.InvalidCredentials);
        }

        if (!user.IsActive)
        {
            return Result.Failure<AuthResponseDto>(IdentityErrors.UserInactive);
        }

        bool isValidPassword = _passwordHasher.Verify(command.Password, user.PasswordHash);
        if (!isValidPassword)
        {
            return Result.Failure<AuthResponseDto>(IdentityErrors.InvalidCredentials);
        }

        // Determine active workspace
        var workspaces = await _workspaceStore.GetUserWorkspacesAsync(user.Id, cancellationToken);
        var activeWorkspace = !string.IsNullOrWhiteSpace(command.WorkspaceSlug)
            ? workspaces.FirstOrDefault(w => w.Slug.Value.Equals(command.WorkspaceSlug, StringComparison.OrdinalIgnoreCase))
            : workspaces.FirstOrDefault();

        string? workspaceRole = null;
        if (activeWorkspace is not null)
        {
            var member = activeWorkspace.Members.FirstOrDefault(m => m.UserId == user.Id);
            workspaceRole = member?.Role.ToString() ?? WorkspaceRole.Member.ToString();
        }

        // Generate tokens
        string accessToken = _tokenGenerator.GenerateAccessToken(user, activeWorkspace, workspaceRole);
        string refreshTokenStr = _tokenGenerator.GenerateRefreshToken();
        var refreshToken = DomainRefreshToken.Create(user.Id, refreshTokenStr, DateTime.UtcNow.AddDays(7));
        await _refreshTokenStore.AddAsync(refreshToken, cancellationToken);

        // Audit log
        await _auditLogStore.AddAsync(AuditLog.Create(
            activeWorkspace?.Id,
            user.Id,
            "User.Login",
            "User",
            user.Id.ToString(),
            $"User {user.Email} successfully logged in."),
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
            refreshTokenStr,
            DateTime.UtcNow.AddMinutes(60),
            userDto,
            workspaceDto);
    }
}
