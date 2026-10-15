namespace CashFlow.Transactions.Domain.Repositories;

using CashFlow.Transactions.Domain.Entities;

public interface IOutboxRepository
{
    Task AddMessageAsync(OutboxMessage message, CancellationToken ct = default);
    Task<IReadOnlyList<OutboxMessage>> GetPendingMessagesAsync(int batchSize, CancellationToken ct = default);
    Task MarkAsProcessedAsync(Guid messageId, CancellationToken ct = default);
}
