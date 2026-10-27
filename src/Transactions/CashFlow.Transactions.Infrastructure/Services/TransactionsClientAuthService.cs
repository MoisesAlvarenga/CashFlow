namespace CashFlow.Transactions.Infrastructure.Services;

using System.Threading;
using System.Threading.Tasks;
using CashFlow.Shared.Contracts.Security;
using CashFlow.Transactions.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Serviço de autenticação de clientes no contexto de Transactions, consultando a tabela ApiClients.
/// </summary>
public class TransactionsClientAuthService : IClientAuthService
{
    private readonly TransactionsDbContext _dbContext;
    private readonly ISecretHasher _hasher;

    public TransactionsClientAuthService(TransactionsDbContext dbContext, ISecretHasher hasher)
    {
        _dbContext = dbContext;
        _hasher = hasher;
    }

    public async Task<ApiClient?> ValidateCredentialsAsync(string clientId, string clientSecret, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            return null;
        }

        var client = await _dbContext.ApiClients
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ClientId == clientId && c.IsActive, cancellationToken);

        if (client == null)
        {
            return null;
        }

        if (!_hasher.VerifySecret(clientSecret, client.SecretHash, client.Salt))
        {
            return null;
        }

        return client;
    }
}
