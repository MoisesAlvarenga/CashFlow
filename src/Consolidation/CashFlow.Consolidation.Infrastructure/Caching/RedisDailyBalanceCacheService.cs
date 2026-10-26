namespace CashFlow.Consolidation.Infrastructure.Caching;

using System.Text.Json;
using CashFlow.Consolidation.Application.DTOs;
using CashFlow.Consolidation.Application.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

/// <summary>
/// Implementação de cache distribuído via IDistributedCache (Redis ou In-Memory fallback).
/// Garante degradação suave (Graceful Degradation) para que falhas transitórias do Redis
/// nunca interrompam as operações de leitura ou escrita do microsserviço.
/// </summary>
public class RedisDailyBalanceCacheService : IDailyBalanceCacheService
{
    private readonly IDistributedCache? _cache;
    private readonly ILogger<RedisDailyBalanceCacheService> _logger;
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(5);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public RedisDailyBalanceCacheService(
        ILogger<RedisDailyBalanceCacheService> logger,
        IDistributedCache? cache = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cache = cache;
    }

    public async Task<DailyBalanceDto?> GetAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        if (_cache == null)
        {
            return null;
        }

        var key = BuildCacheKey(date);
        try
        {
            var bytes = await _cache.GetAsync(key, cancellationToken);
            if (bytes == null || bytes.Length == 0)
            {
                _logger.LogDebug("Cache miss no Redis para chave {CacheKey}.", key);
                return null;
            }

            var dto = JsonSerializer.Deserialize<DailyBalanceDto>(bytes, JsonOptions);
            _logger.LogInformation("Cache hit no Redis para saldo diário de {Date} (Chave: {CacheKey}).", date, key);
            return dto;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao consultar Redis para a chave {CacheKey}. Degradação suave: buscando no banco.", key);
            return null;
        }
    }

    public async Task SetAsync(DailyBalanceDto balance, TimeSpan? ttl = null, CancellationToken cancellationToken = default)
    {
        if (_cache == null || balance == null)
        {
            return;
        }

        var key = BuildCacheKey(balance.Date);
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(balance, JsonOptions);
            var entryOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ttl ?? DefaultTtl
            };

            await _cache.SetAsync(key, bytes, entryOptions, cancellationToken);
            _logger.LogInformation("Saldo diário de {Date} armazenado no Redis com TTL de {Ttl}min (Chave: {CacheKey}).",
                balance.Date, (ttl ?? DefaultTtl).TotalMinutes, key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao gravar chave {CacheKey} no Redis. Degradação suave mantida.", key);
        }
    }

    public async Task RemoveAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        if (_cache == null)
        {
            return;
        }

        var key = BuildCacheKey(date);
        try
        {
            await _cache.RemoveAsync(key, cancellationToken);
            _logger.LogInformation("Invalidação proativa de cache no Redis executada para {Date} (Chave: {CacheKey}).", date, key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao invalidar chave {CacheKey} no Redis.", key);
        }
    }

    private static string BuildCacheKey(DateOnly date) => $"cashflow:daily-balance:{date:yyyy-MM-dd}";
}
