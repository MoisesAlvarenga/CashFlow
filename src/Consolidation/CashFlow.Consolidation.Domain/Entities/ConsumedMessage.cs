namespace CashFlow.Consolidation.Domain.Entities;

public class ConsumedMessage
{
    public Guid TransactionId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public DateTimeOffset ConsumedAt { get; private set; }

    private ConsumedMessage()
    {
    }

    public ConsumedMessage(Guid transactionId, string eventType)
    {
        if (transactionId == Guid.Empty)
        {
            throw new ArgumentException("Transaction ID cannot be empty.", nameof(transactionId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);

        TransactionId = transactionId;
        EventType = eventType;
        ConsumedAt = DateTimeOffset.UtcNow;
    }
}
