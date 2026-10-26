namespace CashFlow.Transactions.Domain.Entities;

/// <summary>
/// Registro de chave de idempotência para garantir que requisições HTTP repetidas
/// com a mesma Idempotency-Key retornem o mesmo resultado sem duplicar lançamentos financeiros.
/// </summary>
public class IdempotencyRecord
{
    public string Key { get; private set; } = string.Empty;
    public Guid TransactionId { get; private set; }
    public string ResponsePayload { get; private set; } = string.Empty;
    public int StatusCode { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private IdempotencyRecord() { }

    public IdempotencyRecord(string key, Guid transactionId, string responsePayload, int statusCode)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("A chave de idempotência não pode ser vazia.", nameof(key));
        }

        Key = key.Trim();
        TransactionId = transactionId;
        ResponsePayload = responsePayload ?? string.Empty;
        StatusCode = statusCode;
        CreatedAt = DateTimeOffset.UtcNow;
    }
}
