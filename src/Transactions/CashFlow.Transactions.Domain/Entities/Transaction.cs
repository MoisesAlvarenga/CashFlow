namespace CashFlow.Transactions.Domain.Entities;

using CashFlow.Shared.Contracts.Events;

public class Transaction
{
    public Guid Id { get; private set; }
    public TransactionType Type { get; private set; }
    public decimal Amount { get; private set; }
    public DateOnly Date { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }

    private Transaction() { }

    public static Transaction CreateCredit(decimal amount, DateOnly date, string description)
    {
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be strictly positive.");

        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description cannot be empty.", nameof(description));

        return new Transaction
        {
            Id = Guid.NewGuid(),
            Type = TransactionType.Credit,
            Amount = amount,
            Date = date,
            Description = description.Trim(),
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public static Transaction CreateDebit(decimal amount, DateOnly date, string description)
    {
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be strictly positive.");

        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description cannot be empty.", nameof(description));

        return new Transaction
        {
            Id = Guid.NewGuid(),
            Type = TransactionType.Debit,
            Amount = amount,
            Date = date,
            Description = description.Trim(),
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}
