using System.Security.Cryptography;
using System.Text;
using IntelliQueryAI.Domain.Services;
using Microsoft.Extensions.Configuration;

namespace IntelliQueryAI.Infrastructure.Security;

/// <summary>
/// Authenticated AES-256 GCM encryption service for securing data source connection credentials.
/// Produces cryptographically random 96-bit nonces and 128-bit authentication tags per operation.
/// </summary>
public sealed class AesEncryptionService : IEncryptionService
{
    private readonly byte[] _key;
    private const int NonceSize = 12; // 96 bits for GCM
    private const int TagSize = 16;   // 128 bits for GCM

    public AesEncryptionService(IConfiguration configuration)
    {
        string rawKey = configuration["Encryption:Key"]
            ?? configuration["Jwt:SecretKey"]
            ?? "IntelliQueryAIEnterpriseSecretKeyMustBe32BytesLongForSecurity2026!";

        // Derive deterministic 256-bit key using SHA-256
        _key = SHA256.HashData(Encoding.UTF8.GetBytes(rawKey));
    }

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
        {
            return string.Empty;
        }

        byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
        byte[] nonce = new byte[NonceSize];
        RandomNumberGenerator.Fill(nonce);

        byte[] cipherBytes = new byte[plainBytes.Length];
        byte[] tag = new byte[TagSize];

        using var aesGcm = new AesGcm(_key, TagSize);
        aesGcm.Encrypt(nonce, plainBytes, cipherBytes, tag);

        // Concatenate: Nonce (12) + Tag (16) + CipherText (N)
        byte[] combined = new byte[NonceSize + TagSize + cipherBytes.Length];
        Buffer.BlockCopy(nonce, 0, combined, 0, NonceSize);
        Buffer.BlockCopy(tag, 0, combined, NonceSize, TagSize);
        Buffer.BlockCopy(cipherBytes, 0, combined, NonceSize + TagSize, cipherBytes.Length);

        return Convert.ToBase64String(combined);
    }

    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
        {
            return string.Empty;
        }

        byte[] combined = Convert.FromBase64String(cipherText);
        if (combined.Length < NonceSize + TagSize)
        {
            throw new CryptographicException("Ciphertext payload is truncated or invalid.");
        }

        byte[] nonce = new byte[NonceSize];
        byte[] tag = new byte[TagSize];
        int cipherLength = combined.Length - NonceSize - TagSize;
        byte[] cipherBytes = new byte[cipherLength];

        Buffer.BlockCopy(combined, 0, nonce, 0, NonceSize);
        Buffer.BlockCopy(combined, NonceSize, tag, 0, TagSize);
        Buffer.BlockCopy(combined, NonceSize + TagSize, cipherBytes, 0, cipherLength);

        byte[] plainBytes = new byte[cipherLength];
        using var aesGcm = new AesGcm(_key, TagSize);
        aesGcm.Decrypt(nonce, cipherBytes, tag, plainBytes);

        return Encoding.UTF8.GetString(plainBytes);
    }
}
