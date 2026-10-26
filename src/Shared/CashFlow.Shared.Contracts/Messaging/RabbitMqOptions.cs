namespace CashFlow.Shared.Contracts.Messaging;

/// <summary>
/// Opções de configuração e topologia do Message Broker RabbitMQ.
/// Compartilhadas entre produtores e consumidores de eventos.
/// </summary>
public class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    /// <summary>
    /// Host do servidor RabbitMQ. Padrão: "localhost" ou "rabbitmq" no Docker.
    /// </summary>
    public string Host { get; set; } = "localhost";

    /// <summary>
    /// Porta AMQP. Padrão: 5672.
    /// </summary>
    public int Port { get; set; } = 5672;

    /// <summary>
    /// Nome de usuário. Padrão: "verity".
    /// </summary>
    public string Username { get; set; } = "verity";

    /// <summary>
    /// Senha de acesso. Padrão: "verity_secret".
    /// </summary>
    public string Password { get; set; } = "verity_secret";

    /// <summary>
    /// Nome da Exchange de publicação de transações.
    /// </summary>
    public string ExchangeName { get; set; } = "cashflow.transactions";

    /// <summary>
    /// Nome da fila consumida pelo serviço de consolidação.
    /// </summary>
    public string QueueName { get; set; } = "cashflow.consolidation.daily-balances";

    /// <summary>
    /// Routing Key para roteamento de eventos de transação registrada.
    /// </summary>
    public string RoutingKey { get; set; } = "transaction.registered";

    /// <summary>
    /// Nome da Dead Letter Exchange (DLX) para mensagens rejeitadas.
    /// </summary>
    public string DeadLetterExchangeName { get; set; } = "cashflow.transactions.dlx";

    /// <summary>
    /// Nome da Dead Letter Queue (DLQ) para mensagens com falha definitiva.
    /// </summary>
    public string DeadLetterQueueName { get; set; } = "cashflow.consolidation.daily-balances.dlq";

    /// <summary>
    /// Routing Key para roteamento de mensagens rejeitadas para a Dead Letter Queue.
    /// </summary>
    public string DeadLetterRoutingKey { get; set; } = "transaction.consolidation.dead";

    /// <summary>
    /// Quantidade máxima de tentativas locais antes do encaminhamento para a Dead Letter Queue.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;
}
