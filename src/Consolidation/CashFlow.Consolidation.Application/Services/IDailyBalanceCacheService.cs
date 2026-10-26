namespace CashFlow.Consolidation.Application.Services;

using CashFlow.Consolidation.Application.DTOs;

/// <summary>
/// Contrato do serviço de cache distribuído para consolidação de saldos diários.
/// Implementa o padrão Cache-Aside com suporte a invalidação proativa orientada a eventos.
/// </summary>
public interface IDailyBalanceCacheService
{
    /// <summary>
    /// Obtém o saldo diário em cache para uma data específica.
    /// Retorna null em caso de Cache Miss ou se o serviço de cache estiver indisponível.
    /// </summary>
    Task<DailyBalanceDto?> GetAsync(DateOnly date, CancellationToken cancellationToken = default);

    /// <summary>
    /// Armazena o saldo diário em cache com TTL opcional (padrão: 5 minutos).
    /// </summary>
    Task SetAsync(DailyBalanceDto balance, TimeSpan? ttl = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalida proativamente a chave de cache do saldo diário da data especificada.
    /// </summary>
    Task RemoveAsync(DateOnly date, CancellationToken cancellationToken = default);
}
