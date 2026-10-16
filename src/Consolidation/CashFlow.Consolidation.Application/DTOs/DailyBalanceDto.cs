namespace CashFlow.Consolidation.Application.DTOs;

public record DailyBalanceDto(
    DateOnly Date,
    decimal TotalCredits,
    decimal TotalDebits,
    decimal NetBalance,
    int TransactionCount,
    DateTimeOffset UpdatedAt
);
