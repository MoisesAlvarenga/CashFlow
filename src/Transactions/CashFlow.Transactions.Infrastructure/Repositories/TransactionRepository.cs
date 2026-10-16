namespace CashFlow.Transactions.Infrastructure.Repositories;

using CashFlow.Transactions.Domain.Entities;
using CashFlow.Transactions.Domain.Repositories;
using CashFlow.Transactions.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

public class TransactionRepository : ITransactionRepository
{
    private readonly TransactionsDbContext _dbContext;

    public TransactionRepository(TransactionsDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task AddAsync(Transaction transaction, CancellationToken ct = default)
    {
        await _dbContext.Transactions.AddAsync(transaction, ct);
        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task<Transaction?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _dbContext.Transactions
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, ct);
    }
}
