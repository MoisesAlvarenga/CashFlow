using CashFlow.Transactions.Domain.Entities;
using Xunit;

namespace CashFlow.UnitTests.Transactions.Domain;

public class OutboxMessageTests
{
    /// <summary>
    /// Garante que a entidade OutboxMessage inicialize com Id gerado, strings vazias, contagem de retentativas zerada e status não processado.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void OutboxMessage_ShouldInitializeWithDefaults()
    {
        // Act
        var message = new OutboxMessage();

        // Assert
        Assert.NotEqual(Guid.Empty, message.Id);
        Assert.Equal(string.Empty, message.EventType);
        Assert.Equal(string.Empty, message.Payload);
        Assert.True(message.CreatedAt <= DateTimeOffset.UtcNow);
        Assert.Null(message.ProcessedAt);
        Assert.Null(message.ErrorMessage);
        Assert.Equal(0, message.RetryCount);
    }

    /// <summary>
    /// Garante que as propriedades de controle da Outbox (RetryCount, ProcessedAt, ErrorMessage, Payload) possam ser atualizadas pelo Worker.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void OutboxMessage_ShouldAllowMutatingProperties()
    {
        // Arrange
        var message = new OutboxMessage
        {
            EventType = "TransactionRegisteredEvent",
            Payload = "{\"id\":\"123\"}",
            ProcessedAt = DateTimeOffset.UtcNow,
            ErrorMessage = "Temporary failure",
            RetryCount = 3
        };

        // Assert
        Assert.Equal("TransactionRegisteredEvent", message.EventType);
        Assert.Equal("{\"id\":\"123\"}", message.Payload);
        Assert.NotNull(message.ProcessedAt);
        Assert.Equal("Temporary failure", message.ErrorMessage);
        Assert.Equal(3, message.RetryCount);
    }
}
