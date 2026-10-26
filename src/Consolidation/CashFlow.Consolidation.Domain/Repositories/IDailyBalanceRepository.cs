namespace CashFlow.Consolidation.Domain.Repositories;

using CashFlow.Consolidation.Domain.Entities;

public interface IDailyBalanceRepository
{
    Task<DailyBalance?> GetByDateAsync(DateOnly date, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically applies an incoming transaction (Credit or Debit) to the daily balance.
    /// In PostgreSQL, executes an atomic upsert (INSERT ... ON CONFLICT ("Date") DO UPDATE)
    /// to eliminate race conditions and lost updates without in-memory read-modify-write.
    /// </summary>
    Task ApplyTransactionAtomicAsync(
        DateOnly date,
        string type,
        decimal amount,
        CancellationToken cancellationToken = default);

    Task SaveOrUpdateAsync(DailyBalance dailyBalance, CancellationToken cancellationToken = default);
}
