namespace CashFlow.Transactions.Domain.Repositories;

using CashFlow.Transactions.Domain.Entities;

public interface IOutboxRepository
{
    Task AddMessageAsync(OutboxMessage message);
    Task<IReadOnlyList<OutboxMessage>> GetPendingMessagesAsync(int batchSize);
    Task MarkAsProcessedAsync(Guid messageId);
}
