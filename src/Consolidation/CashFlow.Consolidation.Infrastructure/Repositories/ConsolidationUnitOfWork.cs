namespace CashFlow.Consolidation.Infrastructure.Repositories;

using CashFlow.Consolidation.Domain.Entities;
using CashFlow.Consolidation.Domain.Repositories;
using CashFlow.Consolidation.Infrastructure.Data;
using CashFlow.Shared.Contracts.Context;
using Microsoft.EntityFrameworkCore;
using Npgsql;

public class ConsolidationUnitOfWork : IConsolidationUnitOfWork
{
    private readonly ConsolidationDbContext _dbContext;
    private readonly ICancellationTokenProvider _tokenProvider;

    public ConsolidationUnitOfWork(
        ConsolidationDbContext dbContext,
        ICancellationTokenProvider? tokenProvider = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _tokenProvider = tokenProvider ?? DefaultCancellationTokenProvider.Instance;
    }

    public async Task<bool> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var ct = cancellationToken.CanBeCanceled ? cancellationToken : _tokenProvider.Token;

        try
        {
            await _dbContext.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException exception) when (IsDuplicateConsumedMessage(exception))
        {
            DetachPendingEntries();
            return false;
        }
    }

    public async Task<bool> ExecuteInTransactionAsync(Func<Task> operation, CancellationToken cancellationToken = default)
    {
        var ct = cancellationToken.CanBeCanceled ? cancellationToken : _tokenProvider.Token;

        if (!_dbContext.Database.IsRelational())
        {
            await operation();
            return await SaveChangesAsync(ct);
        }

        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);
            try
            {
                await operation();
                await _dbContext.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return true;
            }
            catch (DbUpdateException exception) when (IsDuplicateConsumedMessage(exception))
            {
                await transaction.RollbackAsync(ct);
                DetachPendingEntries();
                return false;
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                DetachPendingEntries();
                throw;
            }
        });
    }

    private void DetachPendingEntries()
    {
        foreach (var entry in _dbContext.ChangeTracker.Entries()
                     .Where(entry => entry.State is EntityState.Added or EntityState.Modified)
                     .ToArray())
        {
            entry.State = EntityState.Detached;
        }
    }

    private static bool IsDuplicateConsumedMessage(DbUpdateException exception)
    {
        return exception.Entries.Any(entry => entry.Entity is ConsumedMessage)
            && exception.GetBaseException() is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "PK_ConsumedMessages"
            };
    }
}
