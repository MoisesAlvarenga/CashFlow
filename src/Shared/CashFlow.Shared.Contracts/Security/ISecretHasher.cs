namespace CashFlow.Shared.Contracts.Security;

/// <summary>
/// Contrato para derivação e verificação de hashes seguros de segredos (Client Secret / Senhas)
/// utilizando algoritmos criptográficos robustos recomendados pela OWASP (BCrypt adaptativo).
/// </summary>
public interface ISecretHasher
{
    /// <summary>
    /// Deriva um hash criptográfico seguro e um salt aleatório para o segredo fornecido.
    /// </summary>
    /// <param name="secret">Texto plano do segredo.</param>
    /// <returns>Tupla contendo o hash e o salt codificados em Base64.</returns>
    (string Hash, string Salt) HashSecret(string secret);

    /// <summary>
    /// Verifica se o segredo informado corresponde ao hash e salt persistidos usando comparação de tempo constante.
    /// </summary>
    /// <param name="secret">Texto plano do segredo recebido na requisição.</param>
    /// <param name="hash">Hash em Base64 persistido no banco.</param>
    /// <param name="salt">Salt em Base64 persistido no banco.</param>
    /// <returns>True se válido; caso contrário, False.</returns>
    bool VerifySecret(string secret, string hash, string salt);
}
