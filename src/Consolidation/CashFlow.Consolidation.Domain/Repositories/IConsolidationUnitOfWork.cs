namespace CashFlow.Consolidation.Domain.Repositories;

public interface IConsolidationUnitOfWork
{
    /// <returns>False when the transaction has already been recorded by another concurrent delivery.</returns>
    Task<bool> SaveChangesAsync(CancellationToken cancellationToken = default);
}
