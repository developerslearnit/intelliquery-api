using FluentValidation;
using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Application.Abstractions.Services;
using IntelliQueryAI.Application.Features.Identity.Dtos;
using IntelliQueryAI.Application.Features.Workspaces.Dtos;
using IntelliQueryAI.Domain.Entities.Audit;
using IntelliQueryAI.Domain.Entities.Subscriptions;
using IntelliQueryAI.Domain.Entities.Users;
using IntelliQueryAI.Domain.Entities.Workspaces;
using IntelliQueryAI.Domain.Enums;
using IntelliQueryAI.Domain.Errors;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.Domain.Services;
using IntelliQueryAI.Domain.ValueObjects;
using IntelliQueryAI.SharedKernel.Results;
using DomainRefreshToken = IntelliQueryAI.Domain.Entities.Users.RefreshToken;

namespace IntelliQueryAI.Application.Features.Identity.Commands.RegisterUser;

/// <summary>
/// Command to register a new user along with their primary initial tenant workspace.
/// </summary>
public sealed record RegisterUserCommand(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string WorkspaceName,
    string WorkspaceSlug) : ICommand<Result<AuthResponseDto>>;

public sealed class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email address is required.")
            .EmailAddress().WithMessage("A valid email address is required.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters long.")
            .Matches(@"[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches(@"[a-z]").WithMessage("Password must contain at least one lowercase letter.")
            .Matches(@"[0-9]").WithMessage("Password must contain at least one number.")
            .Matches(@"[\!\?\*\.]").WithMessage("Password must contain at least one special character (!?*.).");

        RuleFor(x => x.FirstName).NotEmpty().WithMessage("First name is required.");
        RuleFor(x => x.LastName).NotEmpty().WithMessage("Last name is required.");
        RuleFor(x => x.WorkspaceName).NotEmpty().WithMessage("Workspace name is required.");
        RuleFor(x => x.WorkspaceSlug)
            .NotEmpty().WithMessage("Workspace slug is required.")
            .MinimumLength(3).WithMessage("Workspace slug must be at least 3 characters.")
            .MaximumLength(63).WithMessage("Workspace slug cannot exceed 63 characters.");
    }
}

public sealed class RegisterUserCommandHandler : ICommandHandler<RegisterUserCommand, Result<AuthResponseDto>>
{
    private readonly IUserStore _userStore;
    private readonly IWorkspaceStore _workspaceStore;
    private readonly ISubscriptionStore _subscriptionStore;
    private readonly IRefreshTokenStore _refreshTokenStore;
    private readonly IAuditLogStore _auditLogStore;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _tokenGenerator;
    private readonly IEmailSender _emailSender;
    private readonly IUnitOfWork _unitOfWork;

    public RegisterUserCommandHandler(
        IUserStore userStore,
        IWorkspaceStore workspaceStore,
        ISubscriptionStore subscriptionStore,
        IRefreshTokenStore refreshTokenStore,
        IAuditLogStore auditLogStore,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator tokenGenerator,
        IEmailSender emailSender,
        IUnitOfWork unitOfWork)
    {
        _userStore = userStore;
        _workspaceStore = workspaceStore;
        _subscriptionStore = subscriptionStore;
        _refreshTokenStore = refreshTokenStore;
        _auditLogStore = auditLogStore;
        _passwordHasher = passwordHasher;
        _tokenGenerator = tokenGenerator;
        _emailSender = emailSender;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<AuthResponseDto>> HandleAsync(RegisterUserCommand command, CancellationToken cancellationToken = default)
    {
        // 1. Validate email address VO
        var emailResult = EmailAddress.Create(command.Email);
        if (emailResult.IsFailure)
        {
            return Result.Failure<AuthResponseDto>(emailResult.Error);
        }

        // 2. Validate tenant slug VO
        var slugResult = TenantSlug.Create(command.WorkspaceSlug);
        if (slugResult.IsFailure)
        {
            return Result.Failure<AuthResponseDto>(slugResult.Error);
        }

        // 3. Check for existing user with this email
        if (await _userStore.EmailExistsAsync(command.Email, cancellationToken))
        {
            return Result.Failure<AuthResponseDto>(IdentityErrors.EmailAlreadyInUse);
        }

        // 4. Check for existing workspace slug
        if (await _workspaceStore.SlugExistsAsync(command.WorkspaceSlug, cancellationToken))
        {
            return Result.Failure<AuthResponseDto>(WorkspaceErrors.SlugAlreadyExists);
        }

        // 5. Create user entity
        string passwordHash = _passwordHasher.Hash(command.Password);
        var userResult = User.Create(
            emailResult.Value,
            passwordHash,
            command.FirstName,
            command.LastName,
            SystemRole.User,
            isEmailVerified: true); // Auto-verify on registration for ease of testing; email token also issued below

        if (userResult.IsFailure)
        {
            return Result.Failure<AuthResponseDto>(userResult.Error);
        }

        var user = userResult.Value;
        await _userStore.AddAsync(user, cancellationToken);

        // 6. Create initial tenant workspace
        var workspaceResult = Workspace.Create(
            command.WorkspaceName,
            slugResult.Value,
            SubscriptionPlan.Free,
            user.Id);

        if (workspaceResult.IsFailure)
        {
            return Result.Failure<AuthResponseDto>(workspaceResult.Error);
        }

        var workspace = workspaceResult.Value;
        await _workspaceStore.AddAsync(workspace, cancellationToken);

        // 7. Create default Free subscription for workspace
        var subscription = Subscription.CreateDefault(workspace.Id, SubscriptionPlan.Free);
        await _subscriptionStore.AddAsync(subscription, cancellationToken);

        // 8. Generate JWT and Refresh Token
        string accessToken = _tokenGenerator.GenerateAccessToken(user, workspace, WorkspaceRole.Owner.ToString());
        string refreshTokenStr = _tokenGenerator.GenerateRefreshToken();
        var refreshToken = DomainRefreshToken.Create(user.Id, refreshTokenStr, DateTime.UtcNow.AddDays(7));
        await _refreshTokenStore.AddAsync(refreshToken, cancellationToken);

        // 9. Generate email verification token
        var emailToken = EmailVerificationToken.Create(user.Id, Guid.NewGuid().ToString("N"), TimeSpan.FromHours(24));
        await _userStore.AddVerificationTokenAsync(emailToken, cancellationToken);

        // 10. Audit log
        await _auditLogStore.AddAsync(AuditLog.Create(
            workspace.Id,
            user.Id,
            "User.Register",
            "User",
            user.Id.ToString(),
            $"User {user.Email} registered and created workspace {workspace.Name} ({workspace.Slug})."),
            cancellationToken);

        // 11. Save transaction
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // 12. Send welcome / verification email asynchronously
        _ = _emailSender.SendEmailAsync(
            user.Email.Value,
            "Welcome to IntelliQueryAI - Verify your account",
            $"Hello {user.FirstName}, your workspace '{workspace.Name}' is ready. Use token {emailToken.Token} to verify.",
            cancellationToken);

        var userDto = new UserDto(
            user.Id,
            user.Email.Value,
            user.FirstName,
            user.LastName,
            user.SystemRole.ToString(),
            user.IsEmailVerified);

        var workspaceDto = new WorkspaceDto(
            workspace.Id,
            workspace.Name,
            workspace.Slug.Value,
            workspace.SubscriptionPlan.ToString(),
            workspace.IsActive,
            workspace.CreatedAtUtc);

        return new AuthResponseDto(
            accessToken,
            refreshTokenStr,
            DateTime.UtcNow.AddMinutes(60),
            userDto,
            workspaceDto);
    }
}
