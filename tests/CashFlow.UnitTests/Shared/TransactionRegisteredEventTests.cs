using System.Text.Json;
using CashFlow.Shared.Contracts.Events;
using Xunit;

namespace CashFlow.UnitTests.Shared;

public class TransactionRegisteredEventTests
{
    /// <summary>
    /// Garante que o evento TransactionRegisteredEvent possa ser serializado em JSON e desserializado sem perda de precisão ou corrupção de tipos.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Event_ShouldSerializeAndDeserializeProperly()
    {
        // Arrange
        var evt = new TransactionRegisteredEvent(
            TransactionId: Guid.NewGuid(),
            Type: nameof(TransactionType.Credit),
            Amount: 150.50m,
            Date: new DateOnly(2026, 10, 15),
            Description: "Payment received",
            Timestamp: DateTimeOffset.UtcNow
        );

        // Act
        var json = JsonSerializer.Serialize(evt);
        var deserialized = JsonSerializer.Deserialize<TransactionRegisteredEvent>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(evt.TransactionId, deserialized.TransactionId);
        Assert.Equal(evt.Type, deserialized.Type);
        Assert.Equal(evt.Amount, deserialized.Amount);
        Assert.Equal(evt.Date, deserialized.Date);
        Assert.Equal(evt.Description, deserialized.Description);
        Assert.Equal(evt.Timestamp, deserialized.Timestamp);
    }

    /// <summary>
    /// Garante a estabilidade contratual dos valores inteiros do enum TransactionType (Credit = 1, Debit = 2) entre microsserviços.
    /// </summary>
    [Theory]
    [InlineData(TransactionType.Credit, 1)]
    [InlineData(TransactionType.Debit, 2)]
    public void TransactionType_ShouldHaveExpectedValues(TransactionType type, int expectedValue)
    {
        Assert.Equal(expectedValue, (int)type);
    }
}
