namespace CashFlow.Shared.Contracts.Events;

public enum TransactionType
{
    Credit = 1,
    Debit = 2
}

public record TransactionRegisteredEvent(
    Guid TransactionId,
    string Type,
    decimal Amount,
    DateOnly Date,
    string Description,
    DateTimeOffset Timestamp
);
