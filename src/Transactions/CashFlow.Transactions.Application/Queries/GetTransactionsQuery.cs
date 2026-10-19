namespace CashFlow.Transactions.Application.Queries;

/// <summary>
/// Query para consulta de lançamentos com filtros opcionais de data.
/// </summary>
/// <param name="Date">Data específica para consulta exata (YYYY-MM-DD).</param>
/// <param name="StartDate">Data inicial do intervalo de consulta (YYYY-MM-DD).</param>
/// <param name="EndDate">Data final do intervalo de consulta (YYYY-MM-DD).</param>
public record GetTransactionsQuery(
    DateOnly? Date = null,
    DateOnly? StartDate = null,
    DateOnly? EndDate = null
);
