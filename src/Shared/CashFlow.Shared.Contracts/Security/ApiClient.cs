namespace CashFlow.Shared.Contracts.Security;

/// <summary>
/// Representa as credenciais de um cliente/aplicação autorizada a gerar tokens de acesso (Client Credentials).
/// O segredo nunca é armazenado em texto plano; utiliza hash PBKDF2 (HMAC-SHA256) com salt criptográfico exclusivo.
/// </summary>
public class ApiClient
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ClientId { get; set; } = string.Empty;
    public string SecretHash { get; set; } = string.Empty;
    public string Salt { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Roles { get; set; } = "Operator";
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}
