namespace CashFlow.Consolidation.Domain.Repositories;

using CashFlow.Consolidation.Domain.Entities;

public interface IConsumedMessageRepository
{
    Task<bool> ExistsAsync(Guid transactionId, CancellationToken cancellationToken = default);
    Task AddAsync(ConsumedMessage message, CancellationToken cancellationToken = default);
}
