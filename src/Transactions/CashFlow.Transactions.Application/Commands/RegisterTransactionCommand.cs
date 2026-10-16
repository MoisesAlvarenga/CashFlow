namespace CashFlow.Transactions.Application.Commands;

using CashFlow.Shared.Contracts.Events;

public record RegisterTransactionCommand(
    TransactionType Type,
    decimal Amount,
    DateOnly Date,
    string Description
);

public record RegisterTransactionResult(
    Guid TransactionId,
    string Status,
    DateTimeOffset RegisteredAt
);
