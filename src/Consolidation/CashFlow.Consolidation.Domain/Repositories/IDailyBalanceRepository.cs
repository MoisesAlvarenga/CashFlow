namespace CashFlow.Consolidation.Domain.Repositories;

using CashFlow.Consolidation.Domain.Entities;

public interface IDailyBalanceRepository
{
    Task<DailyBalance?> GetByDateAsync(DateOnly date, CancellationToken ct = default);
    Task SaveOrUpdateAsync(DailyBalance dailyBalance, CancellationToken ct = default);
}
