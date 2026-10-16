namespace CashFlow.Consolidation.Application.Services;

using CashFlow.Consolidation.Application.DTOs;
using CashFlow.Consolidation.Domain.Entities;
using CashFlow.Consolidation.Domain.Repositories;

public interface IDailyBalanceService
{
    Task<DailyBalanceDto?> GetDailyBalanceAsync(DateOnly date, CancellationToken ct = default);
    Task<DailyBalanceDto> ProcessTransactionAsync(string type, decimal amount, DateOnly date, CancellationToken ct = default);
}

public class DailyBalanceService : IDailyBalanceService
{
    private readonly IDailyBalanceRepository _repository;

    public DailyBalanceService(IDailyBalanceRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<DailyBalanceDto?> GetDailyBalanceAsync(DateOnly date, CancellationToken ct = default)
    {
        var balance = await _repository.GetByDateAsync(date, ct);
        if (balance == null)
        {
            return null;
        }

        return MapToDto(balance);
    }

    public async Task<DailyBalanceDto> ProcessTransactionAsync(string type, decimal amount, DateOnly date, CancellationToken ct = default)
    {
        var balance = await _repository.GetByDateAsync(date, ct) ?? DailyBalance.Create(date);

        if (string.Equals(type, "Credit", StringComparison.OrdinalIgnoreCase))
        {
            balance.AddCredit(amount);
        }
        else if (string.Equals(type, "Debit", StringComparison.OrdinalIgnoreCase))
        {
            balance.AddDebit(amount);
        }
        else
        {
            throw new ArgumentException($"Unsupported transaction type '{type}'.", nameof(type));
        }

        await _repository.SaveOrUpdateAsync(balance, ct);
        return MapToDto(balance);
    }

    private static DailyBalanceDto MapToDto(DailyBalance balance)
    {
        return new DailyBalanceDto(
            Date: balance.Date,
            TotalCredits: balance.TotalCredits,
            TotalDebits: balance.TotalDebits,
            NetBalance: balance.NetBalance,
            TransactionCount: balance.TransactionCount,
            UpdatedAt: balance.UpdatedAt
        );
    }
}
