namespace CashFlow.Transactions.Api.Messaging;

using System.Text;
using CashFlow.Shared.Contracts.Messaging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

public interface IRabbitMqPublisher
{
    Task PublishAsync(string payload, CancellationToken cancellationToken = default);
}

public class RabbitMqPublisher : IRabbitMqPublisher
{
    private readonly IOptionsMonitor<RabbitMqOptions> _optionsMonitor;
    private readonly ILogger<RabbitMqPublisher> _logger;

    public RabbitMqPublisher(
        IOptionsMonitor<RabbitMqOptions> optionsMonitor,
        ILogger<RabbitMqPublisher> logger)
    {
        _optionsMonitor = optionsMonitor ?? throw new ArgumentNullException(nameof(optionsMonitor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task PublishAsync(string payload, CancellationToken cancellationToken = default)
    {
        var options = _optionsMonitor.CurrentValue;
        var factory = new ConnectionFactory
        {
            HostName = options.Host,
            Port = options.Port,
            UserName = options.Username,
            Password = options.Password
        };

        await using var connection = await factory.CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await EnsureTopologyAsync(connection, channel, options, cancellationToken);

        var body = Encoding.UTF8.GetBytes(payload);

        await channel.BasicPublishAsync(
            exchange: options.ExchangeName,
            routingKey: options.RoutingKey,
            body: body,
            cancellationToken: cancellationToken);

        _logger.LogInformation("Mensagem publicada com sucesso no RabbitMQ (Exchange: {Exchange}, Queue: {Queue}).",
            options.ExchangeName, options.QueueName);
    }

    private async Task EnsureTopologyAsync(
        IConnection connection,
        IChannel channel,
        RabbitMqOptions options,
        CancellationToken cancellationToken)
    {
        try
        {
            await DeclareTopologyAsync(channel, options, cancellationToken);
        }
        catch (OperationInterruptedException oiex) when (oiex.ShutdownReason?.ReplyCode == 406)
        {
            _logger.LogWarning("A fila existente '{Queue}' possui argumentos incompatíveis com a topologia DLQ. Recriando a fila...", options.QueueName);
            await using var repairChannel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
            await repairChannel.QueueDeleteAsync(options.QueueName, cancellationToken: cancellationToken);
            await DeclareTopologyAsync(repairChannel, options, cancellationToken);
        }
    }

    private static async Task DeclareTopologyAsync(
        IChannel channel,
        RabbitMqOptions options,
        CancellationToken cancellationToken)
    {
        // 1. Dead Letter Exchange (DLX)
        await channel.ExchangeDeclareAsync(
            exchange: options.DeadLetterExchangeName,
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        // 2. Dead Letter Queue (DLQ)
        await channel.QueueDeclareAsync(
            queue: options.DeadLetterQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        // 3. Bind DLQ to DLX
        await channel.QueueBindAsync(
            queue: options.DeadLetterQueueName,
            exchange: options.DeadLetterExchangeName,
            routingKey: options.DeadLetterRoutingKey,
            cancellationToken: cancellationToken);

        // 4. Main Exchange
        await channel.ExchangeDeclareAsync(
            exchange: options.ExchangeName,
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        // 5. Main Queue with DLX arguments
        var queueArgs = new Dictionary<string, object?>
        {
            ["x-dead-letter-exchange"] = options.DeadLetterExchangeName,
            ["x-dead-letter-routing-key"] = options.DeadLetterRoutingKey
        };

        await channel.QueueDeclareAsync(
            queue: options.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: queueArgs,
            cancellationToken: cancellationToken);

        // 6. Bind Main Queue to Main Exchange
        await channel.QueueBindAsync(
            queue: options.QueueName,
            exchange: options.ExchangeName,
            routingKey: options.RoutingKey,
            cancellationToken: cancellationToken);
    }
}
