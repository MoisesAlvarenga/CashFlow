namespace CashFlow.Transactions.Infrastructure.Repositories;

using CashFlow.Shared.Contracts.Context;
using CashFlow.Transactions.Domain.Entities;
using CashFlow.Transactions.Domain.Repositories;
using CashFlow.Transactions.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

public class IdempotencyRepository : IIdempotencyRepository
{
    private readonly TransactionsDbContext _dbContext;
    private readonly ICancellationTokenProvider _tokenProvider;

    public IdempotencyRepository(
        TransactionsDbContext dbContext,
        ICancellationTokenProvider? tokenProvider = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _tokenProvider = tokenProvider ?? DefaultCancellationTokenProvider.Instance;
    }

    public async Task<IdempotencyRecord?> GetByKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        var ct = cancellationToken.CanBeCanceled ? cancellationToken : _tokenProvider.Token;
        return await _dbContext.IdempotencyRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Key == key, ct);
    }

    public async Task AddAsync(IdempotencyRecord record, CancellationToken cancellationToken = default)
    {
        var ct = cancellationToken.CanBeCanceled ? cancellationToken : _tokenProvider.Token;
        await _dbContext.IdempotencyRecords.AddAsync(record, ct);
    }
}
