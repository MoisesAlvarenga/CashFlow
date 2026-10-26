namespace CashFlow.UnitTests.Consolidation.Application;

using CashFlow.Consolidation.Application.DTOs;
using CashFlow.Consolidation.Application.Services;
using CashFlow.Consolidation.Domain.Entities;
using CashFlow.Consolidation.Domain.Repositories;
using Xunit;

public class DailyBalanceServiceCachingTests
{
    private class FakeDailyBalanceRepository : IDailyBalanceRepository
    {
        public int GetByDateCallCount { get; private set; }
        public DailyBalance? BalanceToReturn { get; set; }

        public Task<DailyBalance?> GetByDateAsync(DateOnly date, CancellationToken cancellationToken = default)
        {
            GetByDateCallCount++;
            return Task.FromResult(BalanceToReturn);
        }

        public Task ApplyTransactionAtomicAsync(DateOnly date, string type, decimal amount, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SaveOrUpdateAsync(DailyBalance dailyBalance, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private class FakeConsumedMessageRepository : IConsumedMessageRepository
    {
        public bool AlreadyExists { get; set; }
        public Task<bool> ExistsAsync(Guid transactionId, CancellationToken cancellationToken = default)
            => Task.FromResult(AlreadyExists);
        public Task AddAsync(ConsumedMessage consumedMessage, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private class FakeUnitOfWork : IConsolidationUnitOfWork
    {
        public Task<bool> SaveChangesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public async Task<bool> ExecuteInTransactionAsync(Func<Task> action, CancellationToken cancellationToken = default)
        {
            await action();
            return true;
        }
    }

    private class FakeDailyBalanceCacheService : IDailyBalanceCacheService
    {
        public readonly Dictionary<DateOnly, DailyBalanceDto> Storage = new();
        public int GetCallCount { get; private set; }
        public int SetCallCount { get; private set; }
        public int RemoveCallCount { get; private set; }
        public DateOnly? LastRemovedDate { get; private set; }

        public Task<DailyBalanceDto?> GetAsync(DateOnly date, CancellationToken cancellationToken = default)
        {
            GetCallCount++;
            Storage.TryGetValue(date, out var dto);
            return Task.FromResult(dto);
        }

        public Task SetAsync(DailyBalanceDto balance, TimeSpan? ttl = null, CancellationToken cancellationToken = default)
        {
            SetCallCount++;
            Storage[balance.Date] = balance;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(DateOnly date, CancellationToken cancellationToken = default)
        {
            RemoveCallCount++;
            LastRemovedDate = date;
            Storage.Remove(date);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task GetDailyBalanceAsync_WhenCached_ReturnsCachedWithoutCallingRepository()
    {
        // Arrange
        var date = new DateOnly(2026, 10, 26);
        var cachedDto = new DailyBalanceDto(date, 500m, 100m, 400m, 3, DateTime.UtcNow);

        var repo = new FakeDailyBalanceRepository();
        var cache = new FakeDailyBalanceCacheService();
        cache.Storage[date] = cachedDto;

        var service = new DailyBalanceService(repo, new FakeConsumedMessageRepository(), new FakeUnitOfWork(), cache);

        // Act
        var result = await service.GetDailyBalanceAsync(date);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(400m, result.NetBalance);
        Assert.Equal(1, cache.GetCallCount);
        Assert.Equal(0, repo.GetByDateCallCount); // Cache hit avoids SQL query
    }

    [Fact]
    public async Task GetDailyBalanceAsync_WhenNotCached_QueriesRepositoryAndPopulatesCache()
    {
        // Arrange
        var date = new DateOnly(2026, 10, 26);
        var entity = DailyBalance.Create(date);
        entity.AddCredit(800m);

        var repo = new FakeDailyBalanceRepository { BalanceToReturn = entity };
        var cache = new FakeDailyBalanceCacheService();

        var service = new DailyBalanceService(repo, new FakeConsumedMessageRepository(), new FakeUnitOfWork(), cache);

        // Act
        var result = await service.GetDailyBalanceAsync(date);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(800m, result.NetBalance);
        Assert.Equal(1, repo.GetByDateCallCount);
        Assert.Equal(1, cache.SetCallCount); // Stored in Redis for subsequent requests
        Assert.True(cache.Storage.ContainsKey(date));
    }

    [Fact]
    public async Task ProcessTransactionAsync_WhenApplied_InvalidatesCacheForTransactionDate()
    {
        // Arrange
        var date = new DateOnly(2026, 10, 26);
        var repo = new FakeDailyBalanceRepository();
        var consumed = new FakeConsumedMessageRepository { AlreadyExists = false };
        var uow = new FakeUnitOfWork();
        var cache = new FakeDailyBalanceCacheService();
        cache.Storage[date] = new DailyBalanceDto(date, 100m, 0m, 100m, 1, DateTime.UtcNow);

        var service = new DailyBalanceService(repo, consumed, uow, cache);

        // Act
        var applied = await service.ProcessTransactionAsync(Guid.NewGuid(), "Credit", 200m, date);

        // Assert
        Assert.True(applied);
        Assert.Equal(1, cache.RemoveCallCount);
        Assert.Equal(date, cache.LastRemovedDate);
        Assert.False(cache.Storage.ContainsKey(date)); // Cache evicted proactively
    }
}
