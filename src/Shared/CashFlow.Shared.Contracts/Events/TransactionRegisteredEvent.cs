namespace CashFlow.Shared.Contracts.Events;

using System.Text.Json.Serialization;

/// <summary>
/// Tipo de operação financeira: Crédito (Credit = 1) ou Débito (Debit = 2).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TransactionType
{
    /// <summary>Crédito - Entrada financeira positiva no fluxo de caixa.</summary>
    Credit = 1,

    /// <summary>Débito - Saída financeira do fluxo de caixa.</summary>
    Debit = 2
}

/// <summary>
/// Evento de domínio publicado após o registro persistido de uma transação financeira.
/// </summary>
/// <param name="TransactionId">Identificador único global (UUID) da transação.</param>
/// <param name="Type">Tipo da transação ("Credit" ou "Debit").</param>
/// <param name="Amount">Valor monetário da transação (positivo, maior que zero).</param>
/// <param name="Date">Data contábil do lançamento financeiro (formato YYYY-MM-DD).</param>
/// <param name="Description">Descrição detalhada do lançamento (máx. 250 caracteres).</param>
/// <param name="Timestamp">Data e hora em UTC de quando o evento foi gerado.</param>
public record TransactionRegisteredEvent(
    Guid TransactionId,
    string Type,
    decimal Amount,
    DateOnly Date,
    string Description,
    DateTimeOffset Timestamp
);
