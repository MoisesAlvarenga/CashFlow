namespace CashFlow.Transactions.Domain.Repositories;

using CashFlow.Transactions.Domain.Entities;

public interface ITransactionRepository
{
    Task AddAsync(Transaction transaction, CancellationToken ct = default);
    Task<Transaction?> GetByIdAsync(Guid id, CancellationToken ct = default);
}
