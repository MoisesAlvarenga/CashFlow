namespace CashFlow.Transactions.Application.Commands;

using CashFlow.Shared.Contracts.Events;

/// <summary>
/// Comando para registrar um novo lançamento financeiro de crédito ou débito.
/// </summary>
/// <param name="Type">Tipo de lançamento: 1 para Crédito (Credit) ou 2 para Débito (Debit).</param>
/// <param name="Amount">Valor monetário da transação. Deve ser estritamente maior que zero com até 2 casas decimais.</param>
/// <param name="Date">Data contábil da transação (formato YYYY-MM-DD).</param>
/// <param name="Description">Descrição detalhada do lançamento (entre 3 e 250 caracteres).</param>
/// <param name="IdempotencyKey">Chave opcional de idempotência enviada via cabeçalho HTTP Idempotency-Key ou no corpo.</param>
public record RegisterTransactionCommand(
    TransactionType Type,
    decimal Amount,
    DateOnly Date,
    string Description,
    string? IdempotencyKey = null
);

/// <summary>
/// Resultado da operação de registro de transação financeira.
/// </summary>
/// <param name="TransactionId">Identificador único gerado para a transação.</param>
/// <param name="Status">Status do processamento inicial (ex: "Registered").</param>
/// <param name="RegisteredAt">Data e hora do registro em UTC.</param>
public record RegisterTransactionResult(
    Guid TransactionId,
    string Status,
    DateTimeOffset RegisteredAt
);
