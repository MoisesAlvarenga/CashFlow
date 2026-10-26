namespace CashFlow.UnitTests.Consolidation.Infrastructure;

using System.Text.Json;
using CashFlow.Consolidation.Application.DTOs;
using CashFlow.Consolidation.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

public class RedisDailyBalanceCacheServiceTests
{
    private readonly IDistributedCache _distributedCache;
    private readonly RedisDailyBalanceCacheService _cacheService;

    public RedisDailyBalanceCacheServiceTests()
    {
        var memoryOptions = Options.Create(new MemoryDistributedCacheOptions());
        _distributedCache = new MemoryDistributedCache(memoryOptions);
        _cacheService = new RedisDailyBalanceCacheService(
            NullLogger<RedisDailyBalanceCacheService>.Instance,
            _distributedCache);
    }

    [Fact]
    public async Task GetAsync_WhenKeyDoesNotExist_ReturnsNull()
    {
        // Arrange
        var date = new DateOnly(2026, 10, 26);

        // Act
        var result = await _cacheService.GetAsync(date);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task SetAsync_And_GetAsync_ReturnsStoredDailyBalance()
    {
        // Arrange
        var date = new DateOnly(2026, 10, 26);
        var dto = new DailyBalanceDto(
            Date: date,
            TotalCredits: 1000m,
            TotalDebits: 250m,
            NetBalance: 750m,
            TransactionCount: 5,
            UpdatedAt: DateTime.UtcNow);

        // Act
        await _cacheService.SetAsync(dto, TimeSpan.FromMinutes(5));
        var cached = await _cacheService.GetAsync(date);

        // Assert
        Assert.NotNull(cached);
        Assert.Equal(dto.Date, cached.Date);
        Assert.Equal(dto.TotalCredits, cached.TotalCredits);
        Assert.Equal(dto.TotalDebits, cached.TotalDebits);
        Assert.Equal(dto.NetBalance, cached.NetBalance);
        Assert.Equal(dto.TransactionCount, cached.TransactionCount);
    }

    [Fact]
    public async Task RemoveAsync_EvictsKeyFromCache()
    {
        // Arrange
        var date = new DateOnly(2026, 10, 26);
        var dto = new DailyBalanceDto(
            Date: date,
            TotalCredits: 500m,
            TotalDebits: 100m,
            NetBalance: 400m,
            TransactionCount: 2,
            UpdatedAt: DateTime.UtcNow);

        await _cacheService.SetAsync(dto);

        // Act
        await _cacheService.RemoveAsync(date);
        var cached = await _cacheService.GetAsync(date);

        // Assert
        Assert.Null(cached);
    }

    private class FaultyDistributedCache : IDistributedCache
    {
        public byte[]? Get(string key) => throw new InvalidOperationException("Redis down");
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => throw new InvalidOperationException("Redis down");
        public void Refresh(string key) => throw new InvalidOperationException("Redis down");
        public Task RefreshAsync(string key, CancellationToken token = default) => throw new InvalidOperationException("Redis down");
        public void Remove(string key) => throw new InvalidOperationException("Redis down");
        public Task RemoveAsync(string key, CancellationToken token = default) => throw new InvalidOperationException("Redis down");
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => throw new InvalidOperationException("Redis down");
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) => throw new InvalidOperationException("Redis down");
    }

    [Fact]
    public async Task GetAsync_WhenDistributedCacheThrows_ReturnsNullGracefully()
    {
        // Arrange
        var faultyService = new RedisDailyBalanceCacheService(
            NullLogger<RedisDailyBalanceCacheService>.Instance,
            new FaultyDistributedCache());

        // Act
        var result = await faultyService.GetAsync(new DateOnly(2026, 10, 26));

        // Assert - Graceful degradation: does not throw, returns null
        Assert.Null(result);
    }

    [Fact]
    public async Task SetAsync_WhenDistributedCacheThrows_DoesNotThrow()
    {
        // Arrange
        var faultyService = new RedisDailyBalanceCacheService(
            NullLogger<RedisDailyBalanceCacheService>.Instance,
            new FaultyDistributedCache());

        var dto = new DailyBalanceDto(
            Date: new DateOnly(2026, 10, 26),
            TotalCredits: 100m,
            TotalDebits: 0m,
            NetBalance: 100m,
            TransactionCount: 1,
            UpdatedAt: DateTime.UtcNow);

        // Act & Assert - does not throw
        var exception = await Record.ExceptionAsync(() => faultyService.SetAsync(dto));
        Assert.Null(exception);
    }
}
