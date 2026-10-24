namespace CashFlow.Transactions.Infrastructure.Repositories;

using CashFlow.Shared.Contracts.Context;
using CashFlow.Transactions.Domain.Repositories;
using CashFlow.Transactions.Infrastructure.Data;

public class TransactionsUnitOfWork : ITransactionsUnitOfWork
{
    private readonly TransactionsDbContext _dbContext;
    private readonly ICancellationTokenProvider _tokenProvider;

    public TransactionsUnitOfWork(
        TransactionsDbContext dbContext,
        ICancellationTokenProvider? tokenProvider = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _tokenProvider = tokenProvider ?? DefaultCancellationTokenProvider.Instance;
    }

    public Task SaveChangesAsync()
    {
        return _dbContext.SaveChangesAsync(_tokenProvider.Token);
    }
}
