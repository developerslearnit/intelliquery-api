using IntelliQueryAI.Domain.Services;

namespace IntelliQueryAI.Infrastructure.Authentication;

/// <summary>
/// BCrypt implementation of password hashing.
/// </summary>
public sealed class PasswordHasher : IPasswordHasher
{
    public string Hash(string password)
    {
        return BCrypt.Net.BCrypt.EnhancedHashPassword(password, workFactor: 11);
    }

    public bool Verify(string password, string passwordHash)
    {
        return BCrypt.Net.BCrypt.EnhancedVerify(password, passwordHash);
    }
}
