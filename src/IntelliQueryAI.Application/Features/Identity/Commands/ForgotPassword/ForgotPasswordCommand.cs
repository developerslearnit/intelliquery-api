using FluentValidation;
using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Application.Abstractions.Services;
using IntelliQueryAI.Domain.Entities.Audit;
using IntelliQueryAI.Domain.Entities.Users;
using IntelliQueryAI.Domain.Errors;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.SharedKernel.Results;

namespace IntelliQueryAI.Application.Features.Identity.Commands.ForgotPassword;

/// <summary>
/// Command to request a password reset email for a given email address.
/// </summary>
public sealed record ForgotPasswordCommand(string Email) : ICommand<Result>;

public sealed class ForgotPasswordCommandValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.");
    }
}

public sealed class ForgotPasswordCommandHandler : ICommandHandler<ForgotPasswordCommand, Result>
{
    private readonly IUserStore _userStore;
    private readonly IEmailSender _emailSender;
    private readonly IAuditLogStore _auditLogStore;
    private readonly IUnitOfWork _unitOfWork;

    public ForgotPasswordCommandHandler(
        IUserStore userStore,
        IEmailSender emailSender,
        IAuditLogStore auditLogStore,
        IUnitOfWork unitOfWork)
    {
        _userStore = userStore;
        _emailSender = emailSender;
        _auditLogStore = auditLogStore;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(ForgotPasswordCommand command, CancellationToken cancellationToken = default)
    {
        var user = await _userStore.GetByEmailAsync(command.Email, cancellationToken);
        // Protect against account enumeration: return success even if user not found
        if (user is null || !user.IsActive)
        {
            return Result.Success();
        }

        var resetToken = PasswordResetToken.Create(user.Id, Guid.NewGuid().ToString("N"), TimeSpan.FromHours(1));
        await _userStore.AddPasswordResetTokenAsync(resetToken, cancellationToken);

        await _auditLogStore.AddAsync(AuditLog.Create(
            null,
            user.Id,
            "User.ForgotPasswordRequested",
            "User",
            user.Id.ToString(),
            $"Password reset requested for {user.Email}."),
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _ = _emailSender.SendEmailAsync(
            user.Email.Value,
            "Reset your IntelliQueryAI password",
            $"Hello {user.FirstName}, your password reset token is: {resetToken.Token}",
            cancellationToken);

        return Result.Success();
    }
}
