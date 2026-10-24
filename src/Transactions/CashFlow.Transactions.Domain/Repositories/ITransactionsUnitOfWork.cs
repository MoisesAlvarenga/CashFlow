namespace CashFlow.Transactions.Domain.Repositories;

public interface ITransactionsUnitOfWork
{
    Task SaveChangesAsync();
}
