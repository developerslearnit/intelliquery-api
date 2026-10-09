namespace IntelliQueryAI.Application.Abstractions.Services;

/// <summary>
/// DTO representing a request to initialize a Paystack payment transaction.
/// </summary>
public sealed record PaystackInitializeRequest(
    string Email,
    int AmountInSubunits,
    string Currency,
    string Reference,
    string CallbackUrl,
    IReadOnlyDictionary<string, string> Metadata);

/// <summary>
/// DTO representing the response from Paystack transaction initialization.
/// </summary>
public sealed record PaystackInitializeResult(
    bool Success,
    string? AuthorizationUrl,
    string? AccessCode,
    string? Reference,
    string? Message);

/// <summary>
/// DTO representing the verification outcome of a Paystack transaction.
/// </summary>
public sealed record PaystackVerifyResult(
    bool Success,
    string Status,
    string Reference,
    int Amount,
    string Currency,
    string? GatewayResponse,
    IReadOnlyDictionary<string, string> Metadata,
    string? Message);

/// <summary>
/// Contract for interfacing with Paystack payment gateway.
/// </summary>
public interface IPaystackService
{
    string PublicKey { get; }

    Task<PaystackInitializeResult> InitializeTransactionAsync(
        PaystackInitializeRequest request,
        CancellationToken cancellationToken = default);

    Task<PaystackVerifyResult> VerifyTransactionAsync(
        string reference,
        CancellationToken cancellationToken = default);

    bool VerifyWebhookSignature(string requestBody, string signature);
}
