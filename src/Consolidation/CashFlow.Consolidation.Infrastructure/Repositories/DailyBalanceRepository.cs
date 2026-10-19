namespace CashFlow.Consolidation.Infrastructure.Repositories;

using CashFlow.Consolidation.Domain.Entities;
using CashFlow.Consolidation.Domain.Repositories;
using CashFlow.Consolidation.Infrastructure.Data;
using CashFlow.Shared.Contracts.Context;
using Microsoft.EntityFrameworkCore;

public class DailyBalanceRepository : IDailyBalanceRepository
{
    private readonly ConsolidationDbContext _dbContext;
    private readonly ICancellationTokenProvider _tokenProvider;

    public DailyBalanceRepository(
        ConsolidationDbContext dbContext,
        ICancellationTokenProvider? tokenProvider = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _tokenProvider = tokenProvider ?? DefaultCancellationTokenProvider.Instance;
    }

    public async Task<DailyBalance?> GetByDateAsync(DateOnly date)
    {
        var ct = _tokenProvider.Token;
        return await _dbContext.DailyBalances
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Date == date, ct);
    }

    public async Task SaveOrUpdateAsync(DailyBalance dailyBalance)
    {
        var ct = _tokenProvider.Token;
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
