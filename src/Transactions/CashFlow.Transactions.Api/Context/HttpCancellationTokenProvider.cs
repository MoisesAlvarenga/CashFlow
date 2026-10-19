namespace CashFlow.Transactions.Api.Context;

using CashFlow.Shared.Contracts.Context;
using Microsoft.AspNetCore.Http;

/// <summary>
/// Provedor de CancellationToken baseado no HttpContext da requisição HTTP ativa.
/// Quando o cliente desconecta ou a requisição sofre timeout, HttpContext.RequestAborted é acionado.
/// </summary>
public class HttpCancellationTokenProvider : ICancellationTokenProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpCancellationTokenProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public CancellationToken Token => _httpContextAccessor.HttpContext?.RequestAborted ?? CancellationToken.None;
}
