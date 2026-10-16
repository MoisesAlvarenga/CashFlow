namespace CashFlow.Transactions.Infrastructure.Repositories;

using CashFlow.Transactions.Domain.Entities;
using CashFlow.Transactions.Domain.Repositories;
using CashFlow.Transactions.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

public class OutboxRepository : IOutboxRepository
{
    private readonly TransactionsDbContext _dbContext;

    public OutboxRepository(TransactionsDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task AddMessageAsync(OutboxMessage message, CancellationToken ct = default)
    {
        await _dbContext.OutboxMessages.AddAsync(message, ct);
        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<OutboxMessage>> GetPendingMessagesAsync(int batchSize, CancellationToken ct = default)
    {
        return await _dbContext.OutboxMessages
            .Where(m => m.ProcessedAt == null)
            .OrderBy(m => m.CreatedAt)
            .Take(batchSize)
            .ToListAsync(ct);
    }

    public async Task MarkAsProcessedAsync(Guid messageId, CancellationToken ct = default)
    {
        var message = await _dbContext.OutboxMessages.FindAsync([messageId], ct);
        if (message != null)
        {
            message.ProcessedAt = DateTimeOffset.UtcNow;
            await _dbContext.SaveChangesAsync(ct);
        }
    }
}
