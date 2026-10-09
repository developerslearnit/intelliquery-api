namespace IntelliQueryAI.Infrastructure.Services.Paystack;

/// <summary>
/// Options for configuring Paystack payment gateway integration.
/// </summary>
public sealed class PaystackOptions
{
    public const string SectionName = "Paystack";

    /// <summary>
    /// Paystack Secret Key (e.g., sk_test_... or sk_live_...).
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// Paystack Public Key (e.g., pk_test_... or pk_live_...).
    /// </summary>
    public string PublicKey { get; set; } = string.Empty;

    /// <summary>
    /// Default payment currency ("USD" or "NGN").
    /// </summary>
    public string Currency { get; set; } = "USD";

    /// <summary>
    /// Default browser return callback URL after external payment authorization.
    /// </summary>
    public string CallbackUrl { get; set; } = "http://localhost:3000/admin";
}
