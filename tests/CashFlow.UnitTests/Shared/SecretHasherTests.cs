namespace CashFlow.UnitTests.Shared;

using System;
using CashFlow.Shared.Contracts.Security;
using Xunit;

public class SecretHasherTests
{
    private readonly SecretHasher _hasher = new();

    [Fact]
    public void HashSecret_WithValidSecret_ShouldReturnBCryptHash()
    {
        // Arrange
        const string secret = "verity-secret";

        // Act
        var (hash, salt) = _hasher.HashSecret(secret);

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(hash));
        Assert.StartsWith("$2", hash);
        Assert.Equal("BCrypt", salt);
        Assert.True(hash.Length >= 59);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void HashSecret_WithNullOrEmpty_ShouldThrowArgumentException(string? invalidSecret)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _hasher.HashSecret(invalidSecret!));
    }

    [Fact]
    public void VerifySecret_WithCorrectSecret_ShouldReturnTrue()
    {
        // Arrange
        const string secret = "verity-secret";
        var (hash, salt) = _hasher.HashSecret(secret);

        // Act
        var isValid = _hasher.VerifySecret(secret, hash, salt);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void VerifySecret_WithIncorrectSecret_ShouldReturnFalse()
    {
        // Arrange
        const string secret = "verity-secret";
        var (hash, salt) = _hasher.HashSecret(secret);

        // Act
        var isValid = _hasher.VerifySecret("wrong-secret-value", hash, salt);

        // Assert
        Assert.False(isValid);
    }

    [Theory]
    [InlineData("", "hash", "salt")]
    [InlineData("secret", "", "salt")]
    [InlineData("secret", "invalid-hash", "salt")]
    public void VerifySecret_WithInvalidInputs_ShouldReturnFalse(string secret, string hash, string salt)
    {
        // Act
        var isValid = _hasher.VerifySecret(secret, hash, salt);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void VerifySecret_WithLegacyPbkdf2Hash_ShouldReturnTrue()
    {
        // Arrange - Hash gerado via PBKDF2 HMAC-SHA256
        const string secret = "verity-secret";
        var saltBytes = new byte[16] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 };
        var hashBytes = System.Security.Cryptography.Rfc2898DeriveBytes.Pbkdf2(
            secret, saltBytes, 100_000, System.Security.Cryptography.HashAlgorithmName.SHA256, 32);
        var legacyHash = Convert.ToBase64String(hashBytes);
        var legacySalt = Convert.ToBase64String(saltBytes);

        // Act
        var isValid = _hasher.VerifySecret(secret, legacyHash, legacySalt);

        // Assert
        Assert.True(isValid);
    }
}
