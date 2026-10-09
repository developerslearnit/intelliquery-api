using FluentValidation;
using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Domain.Entities.Audit;
using IntelliQueryAI.Domain.Errors;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.Domain.Services;
using IntelliQueryAI.SharedKernel.Results;

namespace IntelliQueryAI.Application.Features.Identity.Commands.ResetPassword;

/// <summary>
/// Command to reset an account password using a validated reset token.
/// </summary>
public sealed record ResetPasswordCommand(
    string Token,
    string NewPassword) : ICommand<Result>;

public sealed class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(x => x.Token).NotEmpty().WithMessage("Reset token is required.");
        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("New password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters long.")
            .Matches(@"[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches(@"[a-z]").WithMessage("Password must contain at least one lowercase letter.")
            .Matches(@"[0-9]").WithMessage("Password must contain at least one number.")
            .Matches(@"[\!\?\*\.]").WithMessage("Password must contain at least one special character (!?*.).");
    }
}

public sealed class ResetPasswordCommandHandler : ICommandHandler<ResetPasswordCommand, Result>
{
    private readonly IUserStore _userStore;
    private readonly IRefreshTokenStore _refreshTokenStore;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAuditLogStore _auditLogStore;
    private readonly IUnitOfWork _unitOfWork;

    public ResetPasswordCommandHandler(
        IUserStore userStore,
        IRefreshTokenStore refreshTokenStore,
        IPasswordHasher passwordHasher,
        IAuditLogStore auditLogStore,
        IUnitOfWork unitOfWork)
    {
        _userStore = userStore;
        _refreshTokenStore = refreshTokenStore;
        _passwordHasher = passwordHasher;
        _auditLogStore = auditLogStore;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(ResetPasswordCommand command, CancellationToken cancellationToken = default)
    {
        var resetToken = await _userStore.GetPasswordResetTokenAsync(command.Token, cancellationToken);
        if (resetToken is null || !resetToken.IsValid)
        {
            return Result.Failure(IdentityErrors.InvalidResetToken);
        }

        var user = await _userStore.GetByIdAsync(resetToken.UserId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return Result.Failure(IdentityErrors.UserNotFound);
        }

        string newHash = _passwordHasher.Hash(command.NewPassword);
        user.UpdatePassword(newHash);
        resetToken.MarkAsUsed();

        // Invalidate all active refresh tokens upon password reset for security
        await _refreshTokenStore.RevokeUserTokensAsync(user.Id, cancellationToken);

        await _auditLogStore.AddAsync(AuditLog.Create(
            null,
            user.Id,
            "User.PasswordReset",
            "User",
            user.Id.ToString(),
            $"Password was successfully reset for user {user.Email}."),
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
