namespace CashFlow.Consolidation.Infrastructure.Repositories;

using CashFlow.Consolidation.Domain.Entities;
using CashFlow.Consolidation.Domain.Repositories;
using CashFlow.Consolidation.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

public class ConsumedMessageRepository : IConsumedMessageRepository
{
    private readonly ConsolidationDbContext _dbContext;

    public ConsumedMessageRepository(ConsolidationDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public Task<bool> ExistsAsync(Guid transactionId, CancellationToken cancellationToken = default)
    {
        return _dbContext.ConsumedMessages
            .AnyAsync(message => message.TransactionId == transactionId, cancellationToken);
    }

    public async Task AddAsync(ConsumedMessage message, CancellationToken cancellationToken = default)
    {
        await _dbContext.ConsumedMessages.AddAsync(message, cancellationToken);
    }
}
