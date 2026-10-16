namespace CashFlow.Consolidation.Application.DTOs;

/// <summary>
/// DTO representando o saldo consolidado de uma data específica.
/// </summary>
/// <param name="Date">Data contábil de referência do saldo consolidado (YYYY-MM-DD).</param>
/// <param name="TotalCredits">Soma acumulada de todos os lançamentos de crédito no dia.</param>
/// <param name="TotalDebits">Soma acumulada de todos os lançamentos de débito no dia.</param>
/// <param name="NetBalance">Saldo líquido final do dia (TotalCredits - TotalDebits).</param>
/// <param name="TransactionCount">Quantidade total de lançamentos processados na data.</param>
/// <param name="UpdatedAt">Data e hora da última consolidação/atualização em UTC.</param>
public record DailyBalanceDto(
    DateOnly Date,
    decimal TotalCredits,
    decimal TotalDebits,
    decimal NetBalance,
    int TransactionCount,
    DateTimeOffset UpdatedAt
);
