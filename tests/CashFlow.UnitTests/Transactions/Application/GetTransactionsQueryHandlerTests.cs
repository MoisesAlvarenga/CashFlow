using CashFlow.Shared.Contracts.Common;
using CashFlow.Transactions.Application.DTOs;
using CashFlow.Transactions.Application.Handlers;
using CashFlow.Transactions.Application.Queries;
using CashFlow.Transactions.Domain.Entities;
using CashFlow.Transactions.Domain.Repositories;
using Xunit;

namespace CashFlow.UnitTests.Transactions.Application;

public class GetTransactionsQueryHandlerTests
{
    private class FakeTransactionRepository : ITransactionRepository
    {
        public readonly List<Transaction> Transactions = [];

        public Task AddAsync(Transaction transaction)
        {
            Transactions.Add(transaction);
            return Task.CompletedTask;
        }

        public Task<Transaction?> GetByIdAsync(Guid id)
        {
            return Task.FromResult(Transactions.FirstOrDefault(t => t.Id == id));
        }

        public Task<IReadOnlyList<Transaction>> GetByFilterAsync(DateOnly? startDate, DateOnly? endDate)
        {
            var query = Transactions.AsEnumerable();
            if (startDate.HasValue) query = query.Where(t => t.Date >= startDate.Value);
            if (endDate.HasValue) query = query.Where(t => t.Date <= endDate.Value);
            return Task.FromResult<IReadOnlyList<Transaction>>(query.ToList());
        }

        public Task<(IReadOnlyList<Transaction> Items, int TotalCount)> GetPagedByFilterAsync(
            DateOnly? startDate, DateOnly? endDate, int page, int pageSize)
        {
            var query = Transactions.AsEnumerable();
            if (startDate.HasValue) query = query.Where(t => t.Date >= startDate.Value);
            if (endDate.HasValue) query = query.Where(t => t.Date <= endDate.Value);
            var list = query.ToList();
            var items = list.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            return Task.FromResult<(IReadOnlyList<Transaction> Items, int TotalCount)>((items, list.Count));
        }
    }

    /// <summary>
    /// Garante que a consulta filtrando por uma data específica retorne estritamente os lançamentos daquele dia.
    /// </summary>
    [Fact]
    public async Task HandleAsync_WithSpecificDate_ReturnsOnlyMatchingTransactions()
    {
        // Arrange
        var repo = new FakeTransactionRepository();
        var date1 = new DateOnly(2026, 10, 18);
        var date2 = new DateOnly(2026, 10, 19);

        repo.Transactions.Add(Transaction.CreateCredit(100m, date1, "Venda 1"));
        repo.Transactions.Add(Transaction.CreateDebit(50m, date1, "Compra 1"));
        repo.Transactions.Add(Transaction.CreateCredit(200m, date2, "Venda 2"));

        var handler = new GetTransactionsQueryHandler(repo);
        var query = new GetTransactionsQuery(Date: date1);

        // Act
        var result = await handler.HandleAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
        Assert.All(result.Items, t => Assert.Equal(date1, t.Date));
    }

    /// <summary>
    /// Garante que a consulta filtrando por intervalo de datas (StartDate e EndDate) retorne apenas os lançamentos compreendidos no período.
    /// </summary>
    [Fact]
    public async Task HandleAsync_WithDateRange_ReturnsTransactionsWithinRange()
    {
        // Arrange
        var repo = new FakeTransactionRepository();
        var date1 = new DateOnly(2026, 10, 10);
        var date2 = new DateOnly(2026, 10, 15);
        var date3 = new DateOnly(2026, 10, 20);

        repo.Transactions.Add(Transaction.CreateCredit(100m, date1, "Transação 1"));
        repo.Transactions.Add(Transaction.CreateCredit(200m, date2, "Transação 2"));
        repo.Transactions.Add(Transaction.CreateCredit(300m, date3, "Transação 3"));

        var handler = new GetTransactionsQueryHandler(repo);
        var query = new GetTransactionsQuery(StartDate: new DateOnly(2026, 10, 12), EndDate: new DateOnly(2026, 10, 18));

        // Act
        var result = await handler.HandleAsync(query);

        // Assert
        var item = Assert.Single(result.Items);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(200m, item.Amount);
        Assert.Equal(date2, item.Date);
    }

    /// <summary>
    /// Garante comportamento resiliente com retorno de lista vazia (em vez de null ou erro 500) quando não houver lançamentos no filtro informado.
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenNoTransactionsMatch_ReturnsEmptyList()
    {
        // Arrange
        var repo = new FakeTransactionRepository();
        repo.Transactions.Add(Transaction.CreateCredit(100m, new DateOnly(2026, 10, 18), "Venda 1"));

        var handler = new GetTransactionsQueryHandler(repo);
        var query = new GetTransactionsQuery(Date: new DateOnly(2026, 10, 10));

        // Act
        var result = await handler.HandleAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
    }

    /// <summary>
    /// Garante que uma consulta sem filtros de data retorne a totalidade dos lançamentos cadastrados.
    /// </summary>
    [Fact]
    public async Task HandleAsync_WithNoFilter_ReturnsAllTransactions()
    {
        // Arrange
        var repo = new FakeTransactionRepository();
        repo.Transactions.Add(Transaction.CreateCredit(100m, new DateOnly(2026, 10, 18), "Venda 1"));
        repo.Transactions.Add(Transaction.CreateDebit(50m, new DateOnly(2026, 10, 19), "Despesa 1"));

        var handler = new GetTransactionsQueryHandler(repo);
        var query = new GetTransactionsQuery();

        // Act
        var result = await handler.HandleAsync(query);

        // Assert
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(1, result.Page);
        Assert.Equal(20, result.PageSize);
        Assert.False(result.HasPreviousPage);
        Assert.False(result.HasNextPage);
    }

    /// <summary>
    /// Garante que a paginação funcione adequadamente dividindo os registros e calculando metadados de paginação.
    /// </summary>
    [Fact]
    public async Task HandleAsync_WithPagination_SplitsRecordsAndCalculatesMetadataCorrectly()
    {
        // Arrange
        var repo = new FakeTransactionRepository();
        var baseDate = new DateOnly(2026, 10, 1);
        for (int i = 1; i <= 25; i++)
        {
            repo.Transactions.Add(Transaction.CreateCredit(i * 10m, baseDate.AddDays(i), $"Venda {i}"));
        }

        var handler = new GetTransactionsQueryHandler(repo);

        // Act - Página 1 de tamanho 10
        var page1 = await handler.HandleAsync(new GetTransactionsQuery(Page: 1, PageSize: 10));

        // Assert - Página 1
        Assert.Equal(25, page1.TotalCount);
        Assert.Equal(3, page1.TotalPages);
        Assert.Equal(1, page1.Page);
        Assert.Equal(10, page1.PageSize);
        Assert.Equal(10, page1.Items.Count);
        Assert.False(page1.HasPreviousPage);
        Assert.True(page1.HasNextPage);

        // Act - Página 2 de tamanho 10
        var page2 = await handler.HandleAsync(new GetTransactionsQuery(Page: 2, PageSize: 10));

        // Assert - Página 2
        Assert.Equal(10, page2.Items.Count);
        Assert.Equal(2, page2.Page);
        Assert.True(page2.HasPreviousPage);
        Assert.True(page2.HasNextPage);

        // Act - Página 3 de tamanho 10
        var page3 = await handler.HandleAsync(new GetTransactionsQuery(Page: 3, PageSize: 10));

        // Assert - Página 3 (restante: 5 itens)
        Assert.Equal(5, page3.Items.Count);
        Assert.Equal(3, page3.Page);
        Assert.True(page3.HasPreviousPage);
        Assert.False(page3.HasNextPage);
    }

    /// <summary>
    /// Garante que parâmetros de paginação inválidos (ex: menores que 1 ou excedendo limite de 100) sejam normalizados com segurança.
    /// </summary>
    [Fact]
    public async Task HandleAsync_WithInvalidPaginationParameters_NormalizesToSafeDefaults()
    {
        // Arrange
        var repo = new FakeTransactionRepository();
        repo.Transactions.Add(Transaction.CreateCredit(100m, new DateOnly(2026, 10, 1), "Venda 1"));

        var handler = new GetTransactionsQueryHandler(repo);

        // Act
        var resultZero = await handler.HandleAsync(new GetTransactionsQuery(Page: 0, PageSize: -5));
        var resultExcessive = await handler.HandleAsync(new GetTransactionsQuery(Page: 1, PageSize: 500));

        // Assert
        Assert.Equal(GetTransactionsQuery.DefaultPage, resultZero.Page);
        Assert.Equal(GetTransactionsQuery.DefaultPageSize, resultZero.PageSize);
        Assert.Equal(GetTransactionsQuery.MaxPageSize, resultExcessive.PageSize);
    }

    /// <summary>
    /// Garante a projeção e mapeamento íntegro de todos os campos da entidade de domínio Transaction para o DTO TransactionDto.
    /// </summary>
    [Fact]
    public async Task HandleAsync_CorrectlyMapsToTransactionDto()
    {
        // Arrange
        var repo = new FakeTransactionRepository();
        var date = new DateOnly(2026, 10, 18);
        var transaction = Transaction.CreateCredit(1500.75m, date, "Venda Especial");
        repo.Transactions.Add(transaction);

        var handler = new GetTransactionsQueryHandler(repo);
        var query = new GetTransactionsQuery(Date: date);

        // Act
        var result = await handler.HandleAsync(query);

        // Assert
        var dto = Assert.Single(result.Items);
        Assert.Equal(transaction.Id, dto.Id);
        Assert.Equal("Credit", dto.Type);
        Assert.Equal(1500.75m, dto.Amount);
        Assert.Equal(date, dto.Date);
        Assert.Equal("Venda Especial", dto.Description);
        Assert.Equal(transaction.CreatedAt, dto.CreatedAt);
    }

    /// <summary>
    /// Garante a proteção contra injeção de dependência nula no construtor do handler através de ArgumentNullException.
    /// </summary>
    [Fact]
    public void Constructor_WithNullRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new GetTransactionsQueryHandler(null!));
    }
}
