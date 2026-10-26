namespace CashFlow.Transactions.Infrastructure.Repositories;

using CashFlow.Shared.Contracts.Context;
using CashFlow.Transactions.Domain.Entities;
using CashFlow.Transactions.Domain.Repositories;
using CashFlow.Transactions.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

public class TransactionRepository : ITransactionRepository
{
    private readonly TransactionsDbContext _dbContext;
    private readonly ICancellationTokenProvider _tokenProvider;

    public TransactionRepository(
        TransactionsDbContext dbContext,
        ICancellationTokenProvider? tokenProvider = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _tokenProvider = tokenProvider ?? DefaultCancellationTokenProvider.Instance;
    }

    public async Task AddAsync(Transaction transaction)
    {
        var ct = _tokenProvider.Token;
        await _dbContext.Transactions.AddAsync(transaction, ct);
    }

    public async Task<Transaction?> GetByIdAsync(Guid id)
    {
        var ct = _tokenProvider.Token;
        return await _dbContext.Transactions
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    public async Task<IReadOnlyList<Transaction>> GetByFilterAsync(DateOnly? startDate, DateOnly? endDate)
    {
        var ct = _tokenProvider.Token;
        var query = _dbContext.Transactions.AsNoTracking().AsQueryable();

        if (startDate.HasValue)
        {
            query = query.Where(t => t.Date >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(t => t.Date <= endDate.Value);
        }

        return await query
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<(IReadOnlyList<Transaction> Items, int TotalCount)> GetPagedByFilterAsync(
        DateOnly? startDate, DateOnly? endDate, int page, int pageSize)
    {
        var ct = _tokenProvider.Token;
        var query = _dbContext.Transactions.AsNoTracking().AsQueryable();

        if (startDate.HasValue)
        {
            query = query.Where(t => t.Date >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(t => t.Date <= endDate.Value);
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }
}
