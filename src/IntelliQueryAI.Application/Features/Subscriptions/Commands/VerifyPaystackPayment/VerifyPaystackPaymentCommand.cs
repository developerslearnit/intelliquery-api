using FluentValidation;
using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Application.Abstractions.Services;
using IntelliQueryAI.Application.Features.Subscriptions.Dtos;
using IntelliQueryAI.Domain.Entities.Audit;
using IntelliQueryAI.Domain.Enums;
using IntelliQueryAI.Domain.Errors;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.SharedKernel.Results;

namespace IntelliQueryAI.Application.Features.Subscriptions.Commands.VerifyPaystackPayment;

/// <summary>
/// Command to verify a Paystack payment reference, upgrade the workspace plan, and reset the billing quota.
/// </summary>
public sealed record VerifyPaystackPaymentCommand(string Reference) : ICommand<Result<SubscriptionDto>>;

public sealed class VerifyPaystackPaymentCommandValidator : AbstractValidator<VerifyPaystackPaymentCommand>
{
    public VerifyPaystackPaymentCommandValidator()
    {
        RuleFor(x => x.Reference)
            .NotEmpty().WithMessage("Payment reference is required.")
            .MinimumLength(5).WithMessage("Invalid payment reference format.");
    }
}

public sealed class VerifyPaystackPaymentCommandHandler : ICommandHandler<VerifyPaystackPaymentCommand, Result<SubscriptionDto>>
{
    private readonly IPaystackService _paystackService;
    private readonly ISubscriptionStore _subscriptionStore;
    private readonly IWorkspaceStore _workspaceStore;
    private readonly IAuditLogStore _auditLogStore;
    private readonly IUnitOfWork _unitOfWork;

    public VerifyPaystackPaymentCommandHandler(
        IPaystackService paystackService,
        ISubscriptionStore subscriptionStore,
        IWorkspaceStore workspaceStore,
        IAuditLogStore auditLogStore,
        IUnitOfWork unitOfWork)
    {
        _paystackService = paystackService;
        _subscriptionStore = subscriptionStore;
        _workspaceStore = workspaceStore;
        _auditLogStore = auditLogStore;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<SubscriptionDto>> HandleAsync(
        VerifyPaystackPaymentCommand command,
        CancellationToken cancellationToken = default)
    {
        var verifyResult = await _paystackService.VerifyTransactionAsync(command.Reference, cancellationToken);
        if (!verifyResult.Success || !verifyResult.Status.Equals("success", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<SubscriptionDto>(
                Error.Failure("Payment.VerificationFailed", verifyResult.Message ?? "Transaction verification failed on payment gateway."));
        }

        // 1. Resolve Workspace ID
        Guid workspaceId = Guid.Empty;
        if (verifyResult.Metadata.TryGetValue("workspaceId", out var wsIdStr) && Guid.TryParse(wsIdStr, out var parsedWsId))
        {
            workspaceId = parsedWsId;
        }
        else if (command.Reference.StartsWith("IQ-SUB-", StringComparison.OrdinalIgnoreCase))
        {
            // Extract from standard reference format: IQ-SUB-{Guid:N}-{timestamp}
            var parts = command.Reference.Split('-');
            if (parts.Length >= 3 && Guid.TryParse(parts[2], out var refWsId))
            {
                workspaceId = refWsId;
            }
        }

        if (workspaceId == Guid.Empty)
        {
            return Result.Failure<SubscriptionDto>(PaymentErrors.InvalidReference);
        }

        // 2. Fetch Subscription and Workspace
        var subscription = await _subscriptionStore.GetByWorkspaceIdAsync(workspaceId, cancellationToken);
        if (subscription is null)
        {
            return Result.Failure<SubscriptionDto>(SubscriptionErrors.NotFound);
        }

        var workspace = await _workspaceStore.GetByIdAsync(workspaceId, cancellationToken);

        // 3. Determine Plan Target
        var targetPlan = SubscriptionPlan.Pro;
        if (verifyResult.Metadata.TryGetValue("plan", out var planStr) &&
            Enum.TryParse<SubscriptionPlan>(planStr, true, out var parsedPlan))
        {
            targetPlan = parsedPlan;
        }

        // 4. Update Domain Entities
        subscription.ChangePlan(targetPlan);
        subscription.ResetBillingPeriod(DateTime.UtcNow, DateTime.UtcNow.AddDays(30));

        if (workspace is not null)
        {
            workspace.ChangeSubscriptionPlan(targetPlan);
        }

        // 5. Create Audit Log
        Guid? userIdGuid = null;
        if (verifyResult.Metadata.TryGetValue("userId", out var uStr) && Guid.TryParse(uStr, out var uGuid))
        {
            userIdGuid = uGuid;
        }

        var auditLog = AuditLog.Create(
            workspaceId,
            userIdGuid,
            "Subscription.Upgraded",
            "Subscription",
            subscription.Id.ToString(),
            details: $"Workspace '{workspace?.Name ?? workspaceId.ToString()}' upgraded to {targetPlan} plan via Paystack. Amount: {verifyResult.Amount} {verifyResult.Currency}. Reference: {command.Reference}");

        await _auditLogStore.AddAsync(auditLog, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new SubscriptionDto(
            subscription.WorkspaceId,
            subscription.Plan.ToString(),
            subscription.Status.ToString(),
            subscription.MaxDataSources,
            subscription.MaxQueriesPerMonth,
            subscription.CurrentQueriesCount,
            subscription.CurrentPeriodStartUtc,
            subscription.CurrentPeriodEndUtc);
    }
}
