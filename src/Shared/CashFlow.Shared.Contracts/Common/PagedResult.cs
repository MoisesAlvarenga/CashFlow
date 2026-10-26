namespace CashFlow.Shared.Contracts.Common;

/// <summary>
/// Estrutura de dados genérica para respostas paginadas de coleções na API.
/// </summary>
/// <typeparam name="T">O tipo do item contido na coleção paginada.</typeparam>
/// <param name="Items">Coleção de itens da página atual.</param>
/// <param name="Page">Número da página atual (iniciando em 1).</param>
/// <param name="PageSize">Tamanho máximo de itens por página.</param>
/// <param name="TotalCount">Total acumulado de registros correspondentes ao filtro.</param>
/// <param name="TotalPages">Total de páginas disponíveis.</param>
/// <param name="HasPreviousPage">Indica se existe página anterior.</param>
/// <param name="HasNextPage">Indica se existe próxima página.</param>
public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    bool HasPreviousPage,
    bool HasNextPage
)
{
    /// <summary>
    /// Fábrica para criação de um PagedResult calculando automaticamente TotalPages e flags de navegação.
    /// </summary>
    public static PagedResult<T> Create(IReadOnlyList<T> items, int page, int pageSize, int totalCount)
    {
        var effectivePageSize = pageSize > 0 ? pageSize : 1;
        var totalPages = (int)Math.Ceiling(totalCount / (double)effectivePageSize);

        return new PagedResult<T>(
            Items: items,
            Page: page,
            PageSize: pageSize,
            TotalCount: totalCount,
            TotalPages: totalPages,
            HasPreviousPage: page > 1,
            HasNextPage: page < totalPages
        );
    }
}
