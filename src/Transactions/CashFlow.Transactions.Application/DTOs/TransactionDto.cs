namespace CashFlow.Transactions.Application.DTOs;

/// <summary>
/// DTO representando um lançamento financeiro consultado.
/// </summary>
/// <param name="Id">Identificador único da transação.</param>
/// <param name="Type">Tipo de lançamento (Credit para crédito ou Debit para débito).</param>
/// <param name="Amount">Valor monetário do lançamento.</param>
/// <param name="Date">Data contábil do lançamento (formato YYYY-MM-DD).</param>
/// <param name="Description">Descrição detalhada do lançamento.</param>
/// <param name="CreatedAt">Data e hora do registro em UTC.</param>
public record TransactionDto(
    Guid Id,
    string Type,
    decimal Amount,
    DateOnly Date,
    string Description,
    DateTimeOffset CreatedAt
);
