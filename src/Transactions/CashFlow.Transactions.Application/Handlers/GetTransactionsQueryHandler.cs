namespace CashFlow.Transactions.Application.Handlers;

using CashFlow.Transactions.Application.DTOs;
using CashFlow.Transactions.Application.Queries;
using CashFlow.Transactions.Domain.Repositories;

/// <summary>
/// Contrato do manipulador de consulta de lançamentos financeiros.
/// </summary>
public interface IGetTransactionsQueryHandler
{
    Task<IReadOnlyList<TransactionDto>> HandleAsync(GetTransactionsQuery query);
}

/// <summary>
/// Implementação do manipulador de consulta de lançamentos com filtro por data.
/// </summary>
public class GetTransactionsQueryHandler : IGetTransactionsQueryHandler
{
    private readonly ITransactionRepository _transactionRepository;

    public GetTransactionsQueryHandler(ITransactionRepository transactionRepository)
    {
        _transactionRepository = transactionRepository ?? throw new ArgumentNullException(nameof(transactionRepository));
    }

    public async Task<IReadOnlyList<TransactionDto>> HandleAsync(GetTransactionsQuery query)
    {
        var startDate = query.Date ?? query.StartDate;
        var endDate = query.Date ?? query.EndDate;

        var transactions = await _transactionRepository.GetByFilterAsync(startDate, endDate);

        return transactions.Select(t => new TransactionDto(
            t.Id,
            t.Type.ToString(),
            t.Amount,
            t.Date,
            t.Description,
            t.CreatedAt
        )).ToList();
    }
}
