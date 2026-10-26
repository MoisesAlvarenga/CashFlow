namespace CashFlow.Consolidation.Domain.Repositories;

public interface IConsolidationUnitOfWork
{
    /// <returns>False when the transaction has already been recorded by another concurrent delivery.</returns>
    Task<bool> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the specified operation within an atomic database transaction.
    /// If a duplicate delivery violation occurs, rolls back the transaction and returns false.
    /// </summary>
    Task<bool> ExecuteInTransactionAsync(Func<Task> operation, CancellationToken cancellationToken = default);
}
