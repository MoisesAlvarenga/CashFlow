namespace CashFlow.Consolidation.Domain.Repositories;

using CashFlow.Consolidation.Domain.Entities;

public interface IDailyBalanceRepository
{
    Task<DailyBalance?> GetByDateAsync(DateOnly date);
    Task SaveOrUpdateAsync(DailyBalance dailyBalance);
}
