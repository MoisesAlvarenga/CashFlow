namespace CashFlow.UnitTests.Shared;

using CashFlow.Shared.Contracts.Messaging;
using Xunit;

public class RabbitMqOptionsTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void RabbitMqOptions_DefaultValues_ShouldIncludeDeadLetterQueueConfiguration()
    {
        // Act
        var options = new RabbitMqOptions();

        // Assert
        Assert.Equal("localhost", options.Host);
        Assert.Equal(5672, options.Port);
        Assert.Equal("cashflow.transactions", options.ExchangeName);
        Assert.Equal("cashflow.consolidation.daily-balances", options.QueueName);
        Assert.Equal("transaction.registered", options.RoutingKey);

        // DLQ defaults
        Assert.Equal("cashflow.transactions.dlx", options.DeadLetterExchangeName);
        Assert.Equal("cashflow.consolidation.daily-balances.dlq", options.DeadLetterQueueName);
        Assert.Equal("transaction.consolidation.dead", options.DeadLetterRoutingKey);
        Assert.Equal(3, options.MaxRetryAttempts);
    }
}
