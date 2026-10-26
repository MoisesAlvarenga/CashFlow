namespace CashFlow.Consolidation.Domain.Repositories;

using CashFlow.Consolidation.Domain.Entities;

public interface IDailyBalanceRepository
{
    Task<DailyBalance?> GetByDateAsync(DateOnly date, CancellationToken cancellationToken = default);
    Task SaveOrUpdateAsync(DailyBalance dailyBalance, CancellationToken cancellationToken = default);
}
