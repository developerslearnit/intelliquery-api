using FluentValidation;
using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Domain.Entities.Audit;
using IntelliQueryAI.Domain.Errors;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.SharedKernel.Results;

namespace IntelliQueryAI.Application.Features.Identity.Commands.VerifyEmail;

/// <summary>
/// Command to verify a user's registered email using a one-time verification token.
/// </summary>
public sealed record VerifyEmailCommand(string Token) : ICommand<Result>;

public sealed class VerifyEmailCommandValidator : AbstractValidator<VerifyEmailCommand>
{
    public VerifyEmailCommandValidator()
    {
        RuleFor(x => x.Token).NotEmpty().WithMessage("Verification token is required.");
    }
}

public sealed class VerifyEmailCommandHandler : ICommandHandler<VerifyEmailCommand, Result>
{
    private readonly IUserStore _userStore;
    private readonly IAuditLogStore _auditLogStore;
    private readonly IUnitOfWork _unitOfWork;

    public VerifyEmailCommandHandler(
        IUserStore userStore,
        IAuditLogStore auditLogStore,
        IUnitOfWork unitOfWork)
    {
        _userStore = userStore;
        _auditLogStore = auditLogStore;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(VerifyEmailCommand command, CancellationToken cancellationToken = default)
    {
        var tokenRecord = await _userStore.GetVerificationTokenAsync(command.Token, cancellationToken);
        if (tokenRecord is null || !tokenRecord.IsValid)
        {
            return Result.Failure(IdentityErrors.InvalidVerificationToken);
        }

        var user = await _userStore.GetByIdAsync(tokenRecord.UserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure(IdentityErrors.UserNotFound);
        }

        tokenRecord.MarkAsUsed();
        user.VerifyEmail();

        await _auditLogStore.AddAsync(AuditLog.Create(
            null,
            user.Id,
            "User.VerifyEmail",
            "User",
            user.Id.ToString(),
            $"Email address verified for user {user.Email}."),
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
