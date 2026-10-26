namespace CashFlow.Transactions.Application.Queries;

/// <summary>
/// Query para consulta paginada de lançamentos com filtros opcionais de data.
/// </summary>
/// <param name="Date">Data específica para consulta exata (YYYY-MM-DD).</param>
/// <param name="StartDate">Data inicial do intervalo de consulta (YYYY-MM-DD).</param>
/// <param name="EndDate">Data final do intervalo de consulta (YYYY-MM-DD).</param>
/// <param name="Page">Número da página atual (iniciando em 1).</param>
/// <param name="PageSize">Quantidade de registros por página (mínimo 1, máximo 100).</param>
public record GetTransactionsQuery(
    DateOnly? Date = null,
    DateOnly? StartDate = null,
    DateOnly? EndDate = null,
    int Page = 1,
    int PageSize = 20
)
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
}
