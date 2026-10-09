using System.Security.Cryptography;
using FluentAssertions;
using IntelliQueryAI.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace IntelliQueryAI.Application.Tests;

public sealed class AesEncryptionServiceTests
{
    private readonly AesEncryptionService _encryptionService;

    public AesEncryptionServiceTests()
    {
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["Encryption:Key"]).Returns("TestEncryptionKeyForIntelliQueryPlatform2026!!");
        _encryptionService = new AesEncryptionService(configMock.Object);
    }

    [Fact]
    public void EncryptAndDecrypt_RoundTrip_ShouldRecoverOriginalPlainText()
    {
        // Arrange
        var connectionString = "Host=prod-cluster.internal;Port=5432;Database=finance_db;Username=app_svc;Password=P@ssw0rd12345!;SSLMode=Require;";

        // Act
        var encrypted = _encryptionService.Encrypt(connectionString);
        var decrypted = _encryptionService.Decrypt(encrypted);

        // Assert
        encrypted.Should().NotBeNullOrWhiteSpace();
        encrypted.Should().NotBe(connectionString);
        decrypted.Should().Be(connectionString);
    }

    [Fact]
    public void Encrypt_CalledTwiceOnSameInput_ShouldProduceDifferentCiphertextsDueToRandomNonce()
    {
        // Arrange
        var input = "Server=tcp:sqlserver.database.windows.net,1433;Database=sales;User ID=admin;Password=Secret;";

        // Act
        var cipher1 = _encryptionService.Encrypt(input);
        var cipher2 = _encryptionService.Encrypt(input);

        // Assert
        cipher1.Should().NotBe(cipher2);
        _encryptionService.Decrypt(cipher1).Should().Be(input);
        _encryptionService.Decrypt(cipher2).Should().Be(input);
    }

    [Fact]
    public void Decrypt_WithTamperedCiphertext_ShouldThrowCryptographicException()
    {
        // Arrange
        var input = "Server=localhost;Database=dev;Uid=root;Pwd=root;";
        var encrypted = _encryptionService.Encrypt(input);
        byte[] bytes = Convert.FromBase64String(encrypted);

        // Tamper with the last byte
        bytes[^1] ^= 0xFF;
        string tampered = Convert.ToBase64String(bytes);

        // Act & Assert
        var act = () => _encryptionService.Decrypt(tampered);
        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void EncryptAndDecrypt_EmptyInput_ShouldReturnEmptyString()
    {
        _encryptionService.Encrypt("").Should().BeEmpty();
        _encryptionService.Decrypt("").Should().BeEmpty();
    }
}
