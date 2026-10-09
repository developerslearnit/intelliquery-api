namespace IntelliQueryAI.Application.Features.Subscriptions.Dtos;

/// <summary>
/// DTO returned when initializing a Paystack payment session.
/// </summary>
public sealed record PaystackInitializeDto(
    string AuthorizationUrl,
    string AccessCode,
    string Reference,
    string PublicKey,
    int Amount,
    string Currency,
    string Plan);

/// <summary>
/// DTO providing public client configuration for Paystack inline checkout.
/// </summary>
public sealed record PaystackConfigDto(
    string PublicKey,
    string DefaultCurrency);
