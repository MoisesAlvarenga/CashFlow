namespace CashFlow.Consolidation.Domain.Entities;

public class DailyBalance
{
    public DateOnly Date { get; private set; }
    public decimal TotalCredits { get; private set; }
    public decimal TotalDebits { get; private set; }
    public decimal NetBalance => TotalCredits - TotalDebits;
    public int TransactionCount { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private DailyBalance() { }

    public static DailyBalance Create(DateOnly date)
    {
        return new DailyBalance
        {
            Date = date,
            TotalCredits = 0m,
            TotalDebits = 0m,
            TransactionCount = 0,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    public void AddCredit(decimal amount)
    {
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be strictly positive.");

        TotalCredits += amount;
        TransactionCount++;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void AddDebit(decimal amount)
    {
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be strictly positive.");

        TotalDebits += amount;
        TransactionCount++;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
