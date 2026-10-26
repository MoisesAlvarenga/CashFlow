namespace CashFlow.IntegrationTests.Consolidation;

using CashFlow.Consolidation.Application.Services;
using CashFlow.Consolidation.Infrastructure.Data;
using CashFlow.IntegrationTests.Fixtures;
using CashFlow.Shared.Contracts.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

[Collection("PostgreSqlCollection")]
public class ConsolidationConsumerIntegrationTests : IClassFixture<PostgreSqlFixture>
{
    private readonly PostgreSqlFixture _fixture;

    public ConsolidationConsumerIntegrationTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    [DockerFact]
    public async Task ProcessTransaction_WithSameTransactionId_ShouldApplyOnlyOnceAndIgnoreDuplicateInPostgreSql()
    {
        // Arrange
        using var factory = new ConsolidationWebApplicationFactory(_fixture.ConnectionString);
        await factory.InitializeDatabaseAsync();

        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IDailyBalanceService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ConsolidationDbContext>();

        var transactionId = Guid.NewGuid();
        var testDate = new DateOnly(2026, 11, 1);
        var @event = new TransactionRegisteredEvent(
            TransactionId: transactionId,
            Type: "Credit",
            Amount: 500.00m,
            Date: testDate,
            Description: "Initial credit delivery",
            Timestamp: DateTimeOffset.UtcNow
        );

        // Act 1: Primeira entrega da mensagem
        await service.ProcessTransactionAsync(@event.TransactionId, @event.Type, @event.Amount, @event.Date);

        var balanceAfterFirst = await dbContext.DailyBalances.AsNoTracking().FirstOrDefaultAsync(d => d.Date == testDate);
        balanceAfterFirst.Should().NotBeNull();
        balanceAfterFirst!.TotalCredits.Should().Be(500.00m);
        balanceAfterFirst.TransactionCount.Should().Be(1);

        // Act 2: Reentrega idêntica da mesma mensagem (simulação de at-least-once delivery do RabbitMQ)
        await service.ProcessTransactionAsync(@event.TransactionId, @event.Type, @event.Amount, @event.Date);

        // Assert: O saldo diário NÃO pode ter sido duplicado
        var balanceAfterSecond = await dbContext.DailyBalances.AsNoTracking().FirstOrDefaultAsync(d => d.Date == testDate);
        balanceAfterSecond.Should().NotBeNull();
        balanceAfterSecond!.TotalCredits.Should().Be(500.00m);
        balanceAfterSecond.TransactionCount.Should().Be(1);
        balanceAfterSecond.NetBalance.Should().Be(500.00m);

        // Verifica que o registro de deduplicação existe na tabela ConsumedMessages do PostgreSQL
        var consumed = await dbContext.ConsumedMessages.AsNoTracking().FirstOrDefaultAsync(c => c.TransactionId == transactionId);
        consumed.Should().NotBeNull();
    }

    [DockerFact]
    public async Task ProcessTransaction_ConcurrentDuplicateDeliveries_ShouldSerializeViaPrimaryKeyConstraint()
    {
        // Arrange
        using var factory = new ConsolidationWebApplicationFactory(_fixture.ConnectionString);
        await factory.InitializeDatabaseAsync();

        var transactionId = Guid.NewGuid();
        var testDate = new DateOnly(2026, 11, 2);
        var @event = new TransactionRegisteredEvent(
            TransactionId: transactionId,
            Type: "Debit",
            Amount: 150.00m,
            Date: testDate,
            Description: "Concurrent duplicate debit test",
            Timestamp: DateTimeOffset.UtcNow
        );

        // Act: Dispara duas chamadas exatamente concorrentes usando scopes independentes
        var task1 = Task.Run(async () =>
        {
            using var scope1 = factory.Services.CreateScope();
            var service1 = scope1.ServiceProvider.GetRequiredService<IDailyBalanceService>();
            await service1.ProcessTransactionAsync(@event.TransactionId, @event.Type, @event.Amount, @event.Date);
        });

        var task2 = Task.Run(async () =>
        {
            using var scope2 = factory.Services.CreateScope();
            var service2 = scope2.ServiceProvider.GetRequiredService<IDailyBalanceService>();
            await service2.ProcessTransactionAsync(@event.TransactionId, @event.Type, @event.Amount, @event.Date);
        });

        await Task.WhenAll(task1, task2);

        // Assert: O saldo deve refletir o débito exatamente uma única vez
        using var verifyScope = factory.Services.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<ConsolidationDbContext>();

        var balance = await db.DailyBalances.AsNoTracking().FirstOrDefaultAsync(d => d.Date == testDate);
        balance.Should().NotBeNull();
        balance!.TotalDebits.Should().Be(150.00m);
        balance.TransactionCount.Should().Be(1);
        balance.NetBalance.Should().Be(-150.00m);

        var consumedCount = await db.ConsumedMessages.CountAsync(c => c.TransactionId == transactionId);
        consumedCount.Should().Be(1);
    }
}
