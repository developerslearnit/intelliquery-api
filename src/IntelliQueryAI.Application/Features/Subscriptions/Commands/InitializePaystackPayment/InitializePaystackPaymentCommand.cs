using FluentValidation;
using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Application.Abstractions.Services;
using IntelliQueryAI.Application.Features.Subscriptions.Dtos;
using IntelliQueryAI.Domain.Enums;
using IntelliQueryAI.Domain.Errors;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.SharedKernel.Results;

namespace IntelliQueryAI.Application.Features.Subscriptions.Commands.InitializePaystackPayment;

/// <summary>
/// Command to initialize a Paystack payment session for upgrading a workspace subscription tier.
/// </summary>
public sealed record InitializePaystackPaymentCommand(
    Guid WorkspaceId,
    Guid UserId,
    string Email,
    SubscriptionPlan Plan = SubscriptionPlan.Pro,
    string Currency = "USD",
    string? CallbackUrl = null) : ICommand<Result<PaystackInitializeDto>>;

public sealed class InitializePaystackPaymentCommandValidator : AbstractValidator<InitializePaystackPaymentCommand>
{
    public InitializePaystackPaymentCommandValidator()
    {
        RuleFor(x => x.WorkspaceId).NotEmpty().WithMessage("Workspace identifier is required.");
        RuleFor(x => x.Email).NotEmpty().EmailAddress().WithMessage("A valid billing email is required.");
        RuleFor(x => x.Plan)
            .Must(p => p == SubscriptionPlan.Pro || p == SubscriptionPlan.Enterprise)
            .WithMessage("Only premium plans (Pro or Enterprise) can be purchased via Paystack.");
        RuleFor(x => x.Currency)
            .NotEmpty()
            .Must(c => c.Equals("USD", StringComparison.OrdinalIgnoreCase) || c.Equals("NGN", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Currency must be either USD or NGN.");
    }
}

public sealed class InitializePaystackPaymentCommandHandler : ICommandHandler<InitializePaystackPaymentCommand, Result<PaystackInitializeDto>>
{
    private readonly IWorkspaceStore _workspaceStore;
    private readonly IPaystackService _paystackService;

    public InitializePaystackPaymentCommandHandler(
        IWorkspaceStore workspaceStore,
        IPaystackService paystackService)
    {
        _workspaceStore = workspaceStore;
        _paystackService = paystackService;
    }

    public async Task<Result<PaystackInitializeDto>> HandleAsync(
        InitializePaystackPaymentCommand command,
        CancellationToken cancellationToken = default)
    {
        var workspace = await _workspaceStore.GetByIdAsync(command.WorkspaceId, cancellationToken);
        if (workspace is null)
        {
            return Result.Failure<PaystackInitializeDto>(PaymentErrors.WorkspaceNotFound);
        }

        string currency = command.Currency.ToUpperInvariant();
        int amountInSubunits = (command.Plan, currency) switch
        {
            (SubscriptionPlan.Enterprise, "NGN") => 500_000 * 100, // ₦500,000 in kobo
            (SubscriptionPlan.Enterprise, _) => 499 * 100,         // $499 in cents
            (SubscriptionPlan.Pro, "NGN") => 99_000 * 100,         // ₦99,000 in kobo
            _ => 99 * 100                                          // $99 in cents
        };

        string reference = $"IQ-SUB-{command.WorkspaceId:N}-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        string callbackUrl = !string.IsNullOrWhiteSpace(command.CallbackUrl)
            ? command.CallbackUrl
            : "http://localhost:3000/admin?payment=success";

        var metadata = new Dictionary<string, string>
        {
            ["workspaceId"] = command.WorkspaceId.ToString(),
            ["userId"] = command.UserId.ToString(),
            ["plan"] = command.Plan.ToString(),
            ["workspaceName"] = workspace.Name
        };

        var initRequest = new PaystackInitializeRequest(
            command.Email,
            amountInSubunits,
            currency,
            reference,
            callbackUrl,
            metadata);

        var initResult = await _paystackService.InitializeTransactionAsync(initRequest, cancellationToken);
        if (!initResult.Success || string.IsNullOrWhiteSpace(initResult.AuthorizationUrl))
        {
            return Result.Failure<PaystackInitializeDto>(
                Error.Failure("Payment.InitializationFailed", initResult.Message ?? "Failed to initialize Paystack transaction."));
        }

        return new PaystackInitializeDto(
            initResult.AuthorizationUrl,
            initResult.AccessCode ?? string.Empty,
            initResult.Reference ?? reference,
            _paystackService.PublicKey,
            amountInSubunits,
            currency,
            command.Plan.ToString());
    }
}
