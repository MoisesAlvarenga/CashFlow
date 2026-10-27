namespace CashFlow.Shared.Contracts.Security;

using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Serviço de autenticação e validação de credenciais de clientes cadastrados no banco de dados.
/// </summary>
public interface IClientAuthService
{
    /// <summary>
    /// Valida o ClientId e o ClientSecret contra as credenciais e hashes persistidos no banco.
    /// </summary>
    /// <param name="clientId">Identificador do cliente (ex: "verity-client").</param>
    /// <param name="clientSecret">Segredo do cliente enviado na requisição.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Objeto ApiClient se credenciais válidas e cliente ativo; caso contrário, null.</returns>
    Task<ApiClient?> ValidateCredentialsAsync(string clientId, string clientSecret, CancellationToken cancellationToken = default);
}
