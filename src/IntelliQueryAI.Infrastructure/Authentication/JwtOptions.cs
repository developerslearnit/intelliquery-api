namespace IntelliQueryAI.Infrastructure.Authentication;

/// <summary>
/// Configuration settings for issuing and validating JWT access tokens.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "IntelliQueryAI";
    public string Audience { get; set; } = "IntelliQueryAI-App";
    public string SecretKey { get; set; } = "SuperSecretKeyForIntelliQueryAIEnterprisePlatform2026SecureLong!";
    public int ExpiryMinutes { get; set; } = 60;
}
