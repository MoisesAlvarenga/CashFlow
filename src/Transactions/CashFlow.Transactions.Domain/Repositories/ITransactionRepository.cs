namespace CashFlow.Transactions.Domain.Repositories;

using CashFlow.Transactions.Domain.Entities;

public interface ITransactionRepository
{
    Task AddAsync(Transaction transaction);
    Task<Transaction?> GetByIdAsync(Guid id);
    Task<IReadOnlyList<Transaction>> GetByFilterAsync(DateOnly? startDate, DateOnly? endDate);
    Task<(IReadOnlyList<Transaction> Items, int TotalCount)> GetPagedByFilterAsync(
        DateOnly? startDate, DateOnly? endDate, int page, int pageSize);
}
