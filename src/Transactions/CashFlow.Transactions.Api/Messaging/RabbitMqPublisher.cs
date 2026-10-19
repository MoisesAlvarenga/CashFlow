namespace CashFlow.Transactions.Api.Messaging;

using System.Text;
using CashFlow.Shared.Contracts.Messaging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

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

        await channel.ExchangeDeclareAsync(
            exchange: options.ExchangeName,
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            queue: options.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            queue: options.QueueName,
            exchange: options.ExchangeName,
            routingKey: options.RoutingKey,
            cancellationToken: cancellationToken);

        var body = Encoding.UTF8.GetBytes(payload);

        await channel.BasicPublishAsync(
            exchange: options.ExchangeName,
            routingKey: options.RoutingKey,
            body: body,
            cancellationToken: cancellationToken);

        _logger.LogInformation("Mensagem publicada com sucesso no RabbitMQ (Exchange: {Exchange}, Queue: {Queue}).",
            options.ExchangeName, options.QueueName);
    }
}
