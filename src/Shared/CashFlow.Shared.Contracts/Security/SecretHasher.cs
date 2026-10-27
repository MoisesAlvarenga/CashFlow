namespace CashFlow.Shared.Contracts.Security;

using System;
using BCrypt.Net;

/// <summary>
/// Implementação do ISecretHasher baseada no algoritmo adaptativo BCrypt (via BCrypt.Net-Next)
/// com work factor 11 e salt criptográfico integrado ao hash.
/// </summary>
public class SecretHasher : ISecretHasher
{
    private const int WorkFactor = 11;

    public (string Hash, string Salt) HashSecret(string secret)
    {
        if (string.IsNullOrEmpty(secret))
        {
            throw new ArgumentException("O segredo não pode ser nulo ou vazio.", nameof(secret));
        }

        var hash = BCrypt.HashPassword(secret, workFactor: WorkFactor);
        return (hash, "BCrypt");
    }

    public bool VerifySecret(string secret, string hash, string salt)
    {
        if (string.IsNullOrEmpty(secret) || string.IsNullOrEmpty(hash))
        {
            return false;
        }

        try
        {
            if (hash.StartsWith("$2"))
            {
                return BCrypt.Verify(secret, hash);
            }

            // Fallback de compatibilidade para hashes legados PBKDF2
            var saltBytes = Convert.FromBase64String(salt);
            var expectedHashBytes = Convert.FromBase64String(hash);
            var actualHashBytes = System.Security.Cryptography.Rfc2898DeriveBytes.Pbkdf2(
                secret, saltBytes, 100_000, System.Security.Cryptography.HashAlgorithmName.SHA256, 32);

            return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(expectedHashBytes, actualHashBytes);
        }
        catch
        {
            return false;
        }
    }
}
