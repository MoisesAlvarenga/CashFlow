namespace CashFlow.Consolidation.Application.Services;

using CashFlow.Consolidation.Application.DTOs;
using CashFlow.Consolidation.Domain.Entities;
using CashFlow.Consolidation.Domain.Repositories;
using CashFlow.Shared.Contracts.Events;

public interface IDailyBalanceService
{
    Task<DailyBalanceDto?> GetDailyBalanceAsync(DateOnly date, CancellationToken cancellationToken = default);
    Task<bool> ProcessTransactionAsync(
        Guid transactionId,
        string type,
        decimal amount,
        DateOnly date,
        CancellationToken cancellationToken = default);
}

public class DailyBalanceService : IDailyBalanceService
{
    private readonly IDailyBalanceRepository _repository;
    private readonly IConsumedMessageRepository _consumedMessageRepository;
    private readonly IConsolidationUnitOfWork _unitOfWork;
    private readonly IDailyBalanceCacheService? _cacheService;

    public DailyBalanceService(
        IDailyBalanceRepository repository,
        IConsumedMessageRepository consumedMessageRepository,
        IConsolidationUnitOfWork unitOfWork,
        IDailyBalanceCacheService? cacheService = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _consumedMessageRepository = consumedMessageRepository ?? throw new ArgumentNullException(nameof(consumedMessageRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _cacheService = cacheService;
    }

    public async Task<DailyBalanceDto?> GetDailyBalanceAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        // 1. Consulta ao Cache Distribuído (Cache-Aside)
        if (_cacheService != null)
        {
            var cached = await _cacheService.GetAsync(date, cancellationToken);
            if (cached != null)
            {
                return cached;
            }
        }

        // 2. Consulta à base de dados relacional (Cache Miss)
        var balance = await _repository.GetByDateAsync(date);
        if (balance == null)
        {
            return null;
        }

        var dto = MapToDto(balance);

        // 3. População assíncrona do Cache
        if (_cacheService != null)
        {
            await _cacheService.SetAsync(dto, cancellationToken: cancellationToken);
        }

        return dto;
    }

    public async Task<bool> ProcessTransactionAsync(
        Guid transactionId,
        string type,
        decimal amount,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        if (transactionId == Guid.Empty)
        {
            throw new ArgumentException("Transaction ID cannot be empty.", nameof(transactionId));
        }

        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be strictly positive.");
        }

        if (!string.Equals(type, "Credit", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(type, "Debit", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Unsupported transaction type '{type}'.", nameof(type));
        }

        if (await _consumedMessageRepository.ExistsAsync(transactionId, cancellationToken))
        {
            return false;
        }

        var applied = await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _repository.ApplyTransactionAtomicAsync(date, type, amount, cancellationToken);
            await _consumedMessageRepository.AddAsync(
                new ConsumedMessage(transactionId, nameof(TransactionRegisteredEvent)),
                cancellationToken);
        }, cancellationToken);

        // 4. Invalidação proativa do Cache para garantir consistência imediata
        if (applied && _cacheService != null)
        {
            await _cacheService.RemoveAsync(date, cancellationToken);
        }

        return applied;
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
