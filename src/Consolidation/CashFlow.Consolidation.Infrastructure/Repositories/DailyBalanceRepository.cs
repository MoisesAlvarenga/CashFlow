namespace CashFlow.Consolidation.Infrastructure.Repositories;

using CashFlow.Consolidation.Domain.Entities;
using CashFlow.Consolidation.Domain.Repositories;
using CashFlow.Consolidation.Infrastructure.Data;
using CashFlow.Shared.Contracts.Context;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

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

    public async Task<DailyBalance?> GetByDateAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        var ct = cancellationToken.CanBeCanceled ? cancellationToken : _tokenProvider.Token;
        return await _dbContext.DailyBalances
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Date == date, ct);
    }

    public async Task ApplyTransactionAtomicAsync(
        DateOnly date,
        string type,
        decimal amount,
        CancellationToken cancellationToken = default)
    {
        var ct = cancellationToken.CanBeCanceled ? cancellationToken : _tokenProvider.Token;

        var isCredit = string.Equals(type, "Credit", StringComparison.OrdinalIgnoreCase);
        var isDebit = string.Equals(type, "Debit", StringComparison.OrdinalIgnoreCase);

        if (!isCredit && !isDebit)
        {
            throw new ArgumentException($"Unsupported transaction type '{type}'.", nameof(type));
        }

        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be strictly positive.");
        }

        if (_dbContext.Database.IsNpgsql())
        {
            var credit = isCredit ? amount : 0m;
            var debit = isDebit ? amount : 0m;
            var now = DateTimeOffset.UtcNow;

            const string sql = """
                INSERT INTO "DailyBalances" ("Date", "TotalCredits", "TotalDebits", "TransactionCount", "UpdatedAt")
                VALUES (@pDate, @pCredits, @pDebits, 1, @pNow)
                ON CONFLICT ("Date") DO UPDATE SET
                    "TotalCredits" = "DailyBalances"."TotalCredits" + EXCLUDED."TotalCredits",
                    "TotalDebits" = "DailyBalances"."TotalDebits" + EXCLUDED."TotalDebits",
                    "TransactionCount" = "DailyBalances"."TransactionCount" + 1,
                    "UpdatedAt" = EXCLUDED."UpdatedAt";
                """;

            await _dbContext.Database.ExecuteSqlRawAsync(
                sql,
                [
                    new NpgsqlParameter { ParameterName = "@pDate", NpgsqlDbType = NpgsqlDbType.Date, Value = date },
                    new NpgsqlParameter { ParameterName = "@pCredits", NpgsqlDbType = NpgsqlDbType.Numeric, Value = credit },
                    new NpgsqlParameter { ParameterName = "@pDebits", NpgsqlDbType = NpgsqlDbType.Numeric, Value = debit },
                    new NpgsqlParameter { ParameterName = "@pNow", NpgsqlDbType = NpgsqlDbType.TimestampTz, Value = now }
                ],
                ct);
        }
        else
        {
            // Fallback para provedores não-PostgreSQL (ex.: InMemory nos testes ou desenvolvimento)
            var existing = await _dbContext.DailyBalances.FindAsync([date], ct);
            if (existing == null)
            {
                var newBalance = DailyBalance.Create(date);
                if (isCredit) newBalance.AddCredit(amount);
                else newBalance.AddDebit(amount);
                await _dbContext.DailyBalances.AddAsync(newBalance, ct);
            }
            else
            {
                if (isCredit) existing.AddCredit(amount);
                else existing.AddDebit(amount);
            }
        }
    }

    public async Task SaveOrUpdateAsync(DailyBalance dailyBalance, CancellationToken cancellationToken = default)
    {
        var ct = cancellationToken.CanBeCanceled ? cancellationToken : _tokenProvider.Token;
        var existing = await _dbContext.DailyBalances.FindAsync([dailyBalance.Date], ct);
        if (existing == null)
        {
            await _dbContext.DailyBalances.AddAsync(dailyBalance, ct);
        }
        else
        {
            _dbContext.Entry(existing).CurrentValues.SetValues(dailyBalance);
        }
    }
}
