namespace CashFlow.Consolidation.Infrastructure.Repositories;

using CashFlow.Consolidation.Domain.Entities;
using CashFlow.Consolidation.Domain.Repositories;
using CashFlow.Consolidation.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

public class DailyBalanceRepository : IDailyBalanceRepository
{
    private readonly ConsolidationDbContext _dbContext;

    public DailyBalanceRepository(ConsolidationDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<DailyBalance?> GetByDateAsync(DateOnly date, CancellationToken ct = default)
    {
        return await _dbContext.DailyBalances
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Date == date, ct);
    }

    public async Task SaveOrUpdateAsync(DailyBalance dailyBalance, CancellationToken ct = default)
    {
        var existing = await _dbContext.DailyBalances.FindAsync([dailyBalance.Date], ct);
        if (existing == null)
        {
            await _dbContext.DailyBalances.AddAsync(dailyBalance, ct);
        }
        else
        {
            _dbContext.Entry(existing).CurrentValues.SetValues(dailyBalance);
        }

        await _dbContext.SaveChangesAsync(ct);
    }
}
