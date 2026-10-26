namespace CashFlow.Shared.Contracts.Security;

/// <summary>
/// Requisição para obtenção de token de acesso JWT (Client Credentials / Dev).
/// </summary>
/// <param name="ClientId">Identificador do cliente ou operador (ex: "verity-client").</param>
/// <param name="ClientSecret">Segredo ou senha de autenticação (ex: "verity-secret").</param>
public record TokenRequest(
    string ClientId,
    string ClientSecret
);

/// <summary>
/// Resposta com o token JWT emitido com sucesso.
/// </summary>
/// <param name="AccessToken">Token JWT assinado.</param>
/// <param name="TokenType">Tipo de autorização HTTP (padrão: "Bearer").</param>
/// <param name="ExpiresInSeconds">Tempo de vida do token em segundos.</param>
public record TokenResponse(
    string AccessToken,
    string TokenType,
    int ExpiresInSeconds
);
