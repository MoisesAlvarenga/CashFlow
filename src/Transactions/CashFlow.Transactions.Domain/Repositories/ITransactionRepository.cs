namespace CashFlow.Transactions.Domain.Repositories;

using CashFlow.Transactions.Domain.Entities;

public interface ITransactionRepository
{
    Task AddAsync(Transaction transaction);
    Task<Transaction?> GetByIdAsync(Guid id);
    Task<IReadOnlyList<Transaction>> GetByFilterAsync(DateOnly? startDate, DateOnly? endDate);
}
