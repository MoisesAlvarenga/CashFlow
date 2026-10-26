namespace CashFlow.Transactions.Application.Handlers;

using CashFlow.Shared.Contracts.Common;
using CashFlow.Transactions.Application.DTOs;
using CashFlow.Transactions.Application.Queries;
using CashFlow.Transactions.Domain.Repositories;

/// <summary>
/// Contrato do manipulador de consulta paginada de lançamentos financeiros.
/// </summary>
public interface IGetTransactionsQueryHandler
{
    Task<PagedResult<TransactionDto>> HandleAsync(GetTransactionsQuery query);
}

/// <summary>
/// Implementação do manipulador de consulta de lançamentos com filtro por data e paginação.
/// </summary>
public class GetTransactionsQueryHandler : IGetTransactionsQueryHandler
{
    private readonly ITransactionRepository _transactionRepository;

    public GetTransactionsQueryHandler(ITransactionRepository transactionRepository)
    {
        _transactionRepository = transactionRepository ?? throw new ArgumentNullException(nameof(transactionRepository));
    }

    public async Task<PagedResult<TransactionDto>> HandleAsync(GetTransactionsQuery query)
    {
        var startDate = query.Date ?? query.StartDate;
        var endDate = query.Date ?? query.EndDate;

        var page = query.Page < 1 ? GetTransactionsQuery.DefaultPage : query.Page;
        var pageSize = query.PageSize < 1
            ? GetTransactionsQuery.DefaultPageSize
            : Math.Min(query.PageSize, GetTransactionsQuery.MaxPageSize);

        var (transactions, totalCount) = await _transactionRepository.GetPagedByFilterAsync(
            startDate, endDate, page, pageSize);

        var dtos = transactions.Select(t => new TransactionDto(
            t.Id,
            t.Type.ToString(),
            t.Amount,
            t.Date,
            t.Description,
            t.CreatedAt
        )).ToList();

        return PagedResult<TransactionDto>.Create(dtos, page, pageSize, totalCount);
    }
}
