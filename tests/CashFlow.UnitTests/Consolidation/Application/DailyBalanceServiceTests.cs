using CashFlow.Consolidation.Application.Services;
using CashFlow.Consolidation.Domain.Entities;
using CashFlow.Consolidation.Domain.Repositories;
using Xunit;

namespace CashFlow.UnitTests.Consolidation.Application;

public class DailyBalanceServiceTests
{
    private class FakeDailyBalanceRepository : IDailyBalanceRepository
    {
        public readonly Dictionary<DateOnly, DailyBalance> Storage = new();

        public Task<DailyBalance?> GetByDateAsync(DateOnly date)
        {
            Storage.TryGetValue(date, out var balance);
            return Task.FromResult(balance);
        }

        public Task SaveOrUpdateAsync(DailyBalance dailyBalance)
        {
            Storage[dailyBalance.Date] = dailyBalance;
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Garante que a consulta de saldo consolidado retorne null quando não houver registros para a data (gerando HTTP 404 padronizado na API).
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetDailyBalanceAsync_WhenNotFound_ShouldReturnNull()
    {
        // Arrange
        var repo = new FakeDailyBalanceRepository();
        var service = new DailyBalanceService(repo);

        // Act
        var result = await service.GetDailyBalanceAsync(new DateOnly(2026, 10, 16));

        // Assert
        Assert.Null(result);
    }

    /// <summary>
    /// Garante o correto mapeamento da entidade DailyBalance para DailyBalanceDto contendo data, créditos, débitos, saldo líquido e contagem de lançamentos.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetDailyBalanceAsync_WhenExists_ShouldReturnMappedDto()
    {
        // Arrange
        var repo = new FakeDailyBalanceRepository();
        var targetDate = new DateOnly(2026, 10, 16);
        var existingBalance = DailyBalance.Create(targetDate);
        existingBalance.AddCredit(300.00m);
        existingBalance.AddDebit(100.00m);
        await repo.SaveOrUpdateAsync(existingBalance);

        var service = new DailyBalanceService(repo);

        // Act
        var result = await service.GetDailyBalanceAsync(targetDate);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(targetDate, result.Date);
        Assert.Equal(300.00m, result.TotalCredits);
        Assert.Equal(100.00m, result.TotalDebits);
        Assert.Equal(200.00m, result.NetBalance);
        Assert.Equal(2, result.TransactionCount);
    }

    /// <summary>
    /// Garante que o recebimento de evento de crédito crie um novo saldo diário se inexistente e acumule o valor corretamente.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task ProcessTransactionAsync_WithCredit_ShouldCreateAndAccumulateBalance()
    {
        // Arrange
        var repo = new FakeDailyBalanceRepository();
        var service = new DailyBalanceService(repo);
        var date = new DateOnly(2026, 10, 16);

        // Act
        var result = await service.ProcessTransactionAsync("Credit", 150.00m, date);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(150.00m, result.TotalCredits);
        Assert.Equal(0m, result.TotalDebits);
        Assert.Equal(150.00m, result.NetBalance);
        Assert.Equal(1, result.TransactionCount);
        Assert.True(repo.Storage.ContainsKey(date));
    }

    /// <summary>
    /// Garante a agregação incremental de débitos em saldo diário pré-existente sem sobrescrever lançamentos anteriores.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task ProcessTransactionAsync_WithDebit_ShouldAccumulateToExistingBalance()
    {
        // Arrange
        var repo = new FakeDailyBalanceRepository();
        var service = new DailyBalanceService(repo);
        var date = new DateOnly(2026, 10, 16);

        // Act
        await service.ProcessTransactionAsync("Credit", 200.00m, date);
        var finalResult = await service.ProcessTransactionAsync("Debit", 80.00m, date);

        // Assert
        Assert.Equal(200.00m, finalResult.TotalCredits);
        Assert.Equal(80.00m, finalResult.TotalDebits);
        Assert.Equal(120.00m, finalResult.NetBalance);
        Assert.Equal(2, finalResult.TransactionCount);
    }

    /// <summary>
    /// Garante a rejeição de tipos de transação desconhecidos com ArgumentException para evitar corrupção do consolidado financeiro.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task ProcessTransactionAsync_WithInvalidType_ShouldThrowArgumentException()
    {
        // Arrange
        var repo = new FakeDailyBalanceRepository();
        var service = new DailyBalanceService(repo);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ProcessTransactionAsync("UnknownType", 100.00m, new DateOnly(2026, 10, 16)));
    }
}
