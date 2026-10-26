namespace CashFlow.IntegrationTests.Consolidation;

using CashFlow.Consolidation.Domain.Repositories;
using CashFlow.Consolidation.Infrastructure.Data;
using CashFlow.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

[Collection("PostgreSqlCollection")]
public class DailyBalanceConcurrencyIntegrationTests : IClassFixture<PostgreSqlFixture>
{
    private readonly PostgreSqlFixture _fixture;

    public DailyBalanceConcurrencyIntegrationTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    [DockerFact]
    public async Task ApplyTransactionAtomicAsync_WithHighConcurrency_ShouldPreventLostUpdatesAndComputeExactTotals()
    {
        // Arrange
        using var factory = new ConsolidationWebApplicationFactory(_fixture.ConnectionString);
        await factory.InitializeDatabaseAsync();

        var targetDate = new DateOnly(2026, 11, 10);
        const int creditTransactionsCount = 15;
        const decimal creditAmount = 100.00m;
        const int debitTransactionsCount = 15;
        const decimal debitAmount = 40.00m;

        var tasks = new List<Task>();

        // Act: 30 transações disparadas simultaneamente para a mesma data contábil
        for (var i = 0; i < creditTransactionsCount; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                using var scope = factory.Services.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IDailyBalanceRepository>();
                var uow = scope.ServiceProvider.GetRequiredService<IConsolidationUnitOfWork>();

                await uow.ExecuteInTransactionAsync(async () =>
                {
                    await repository.ApplyTransactionAtomicAsync(targetDate, "Credit", creditAmount);
                });
            }));
        }

        for (var i = 0; i < debitTransactionsCount; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                using var scope = factory.Services.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IDailyBalanceRepository>();
                var uow = scope.ServiceProvider.GetRequiredService<IConsolidationUnitOfWork>();

                await uow.ExecuteInTransactionAsync(async () =>
                {
                    await repository.ApplyTransactionAtomicAsync(targetDate, "Debit", debitAmount);
                });
            }));
        }

        await Task.WhenAll(tasks);

        // Assert: Validação matemática estrita no PostgreSQL
        using var verifyScope = factory.Services.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<ConsolidationDbContext>();

        var balance = await db.DailyBalances.AsNoTracking().FirstOrDefaultAsync(d => d.Date == targetDate);

        balance.Should().NotBeNull();
        balance!.TotalCredits.Should().Be(creditTransactionsCount * creditAmount, "nenhum crédito pode ser perdido por lost update");
        balance.TotalDebits.Should().Be(debitTransactionsCount * debitAmount, "nenhum débito pode ser perdido por lost update");
        balance.TransactionCount.Should().Be(creditTransactionsCount + debitTransactionsCount);
        balance.NetBalance.Should().Be((creditTransactionsCount * creditAmount) - (debitTransactionsCount * debitAmount));
    }
}
