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
        Assert.Equal(2, result.Count);
        Assert.All(result, t => Assert.Equal(date1, t.Date));
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
        Assert.Single(result);
        Assert.Equal(200m, result[0].Amount);
        Assert.Equal(date2, result[0].Date);
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
        Assert.Empty(result);
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
        Assert.Equal(2, result.Count);
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
        var dto = Assert.Single(result);
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
