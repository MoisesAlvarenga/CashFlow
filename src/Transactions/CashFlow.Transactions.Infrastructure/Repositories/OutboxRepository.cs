namespace CashFlow.Transactions.Infrastructure.Repositories;

using CashFlow.Shared.Contracts.Context;
using CashFlow.Transactions.Domain.Entities;
using CashFlow.Transactions.Domain.Repositories;
using CashFlow.Transactions.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

public class OutboxRepository : IOutboxRepository
{
    private readonly TransactionsDbContext _dbContext;
    private readonly ICancellationTokenProvider _tokenProvider;

    public OutboxRepository(
        TransactionsDbContext dbContext,
        ICancellationTokenProvider? tokenProvider = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _tokenProvider = tokenProvider ?? DefaultCancellationTokenProvider.Instance;
    }

    public async Task AddMessageAsync(OutboxMessage message)
    {
        var ct = _tokenProvider.Token;
        await _dbContext.OutboxMessages.AddAsync(message, ct);
    }

    public async Task<IReadOnlyList<OutboxMessage>> GetPendingMessagesAsync(int batchSize)
    {
        var ct = _tokenProvider.Token;
        return await _dbContext.OutboxMessages
            .Where(m => m.ProcessedAt == null)
            .OrderBy(m => m.CreatedAt)
            .Take(batchSize)
            .ToListAsync(ct);
    }

    public async Task MarkAsProcessedAsync(Guid messageId)
    {
        var ct = _tokenProvider.Token;
        var message = await _dbContext.OutboxMessages.FindAsync([messageId], ct);
        if (message != null)
        {
            message.ProcessedAt = DateTimeOffset.UtcNow;
            await _dbContext.SaveChangesAsync(ct);
        }
    }
}
