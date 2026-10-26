namespace CashFlow.Shared.Contracts.Security;

/// <summary>
/// Opções de configuração para autenticação e validação de tokens JWT Bearer.
/// </summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>
    /// Emissor válido do token JWT. Padrão: "CashFlow".
    /// </summary>
    public string Issuer { get; set; } = "CashFlow";

    /// <summary>
    /// Audiência esperada do token. Padrão: "CashFlow.Api".
    /// </summary>
    public string Audience { get; set; } = "CashFlow.Api";

    /// <summary>
    /// Chave simétrica secreta HMAC-SHA256 (mínimo de 256 bits).
    /// </summary>
    public string SecretKey { get; set; } = "SuperSecretKeyForCashFlowDevelopmentPurposes12345!";

    /// <summary>
    /// Tempo de expiração do token em minutos. Padrão: 60 minutos.
    /// </summary>
    public int ExpirationMinutes { get; set; } = 60;
}
