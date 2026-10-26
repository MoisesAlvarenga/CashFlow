namespace CashFlow.IntegrationTests.Messaging;

using System.Text;
using System.Text.Json;
using CashFlow.IntegrationTests.Fixtures;
using CashFlow.Shared.Contracts.Events;
using CashFlow.Shared.Contracts.Messaging;
using CashFlow.Transactions.Api.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Xunit;

public class RabbitMqEndToEndIntegrationTests : IClassFixture<RabbitMqFixture>
{
    private readonly RabbitMqFixture _fixture;

    public RabbitMqEndToEndIntegrationTests(RabbitMqFixture fixture)
    {
        _fixture = fixture;
    }

    [DockerFact]
    public async Task PublisherConfirms_WhenPublishingToRabbitMq_ShouldConfirmDelivery()
    {
        // Arrange
        var options = new RabbitMqOptions
        {
            Host = _fixture.HostName,
            Port = _fixture.Port,
            Username = _fixture.UserName,
            Password = _fixture.Password,
            ExchangeName = "cashflow.integration.exchange",
            RoutingKey = "cashflow.integration.key",
            QueueName = "cashflow.integration.queue",
            DeadLetterExchangeName = "cashflow.integration.dlx",
            DeadLetterQueueName = "cashflow.integration.dlq",
            DeadLetterRoutingKey = "cashflow.integration.dlq.key"
        };

        var optionsMonitor = new TestOptionsMonitor<RabbitMqOptions>(options);
        await using var publisher = new RabbitMqPublisher(optionsMonitor, NullLogger<RabbitMqPublisher>.Instance);

        var integrationEvent = new TransactionRegisteredEvent(
            TransactionId: Guid.NewGuid(),
            Type: "Credit",
            Amount: 2500.00m,
            Date: new DateOnly(2026, 11, 15),
            Description: "End-to-end publisher confirm test",
            Timestamp: DateTimeOffset.UtcNow
        );

        var payload = JsonSerializer.Serialize(integrationEvent);

        // Act: Publicação com Publisher Confirms habilitado
        var act = async () => await publisher.PublishAsync(payload);

        // Assert: Deve completar com sucesso após receber ACK do RabbitMQ real
        await act.Should().NotThrowAsync();
    }

    [DockerFact]
    public async Task DeadLetterQueue_WhenMessageNackedWithRequeueFalse_ShouldRouteToDlq()
    {
        // Arrange
        var factory = new ConnectionFactory
        {
            HostName = _fixture.HostName,
            Port = _fixture.Port,
            UserName = _fixture.UserName,
            Password = _fixture.Password
        };

        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        var exchange = "cashflow.test.dlq.exchange";
        var queue = "cashflow.test.main.queue";
        var routingKey = "cashflow.test.key";
        var dlx = "cashflow.test.dlx";
        var dlq = "cashflow.test.dlq";
        var dlqRoutingKey = "cashflow.test.dlq.key";

        // 1. Declara Dead Letter Exchange e DLQ
        await channel.ExchangeDeclareAsync(dlx, ExchangeType.Direct, durable: true);
        await channel.QueueDeclareAsync(dlq, durable: true, exclusive: false, autoDelete: false);
        await channel.QueueBindAsync(dlq, dlx, dlqRoutingKey);

        // 2. Declara Exchange principal e Fila configurada com x-dead-letter-exchange
        await channel.ExchangeDeclareAsync(exchange, ExchangeType.Direct, durable: true);
        var queueArgs = new Dictionary<string, object?>
        {
            ["x-dead-letter-exchange"] = dlx,
            ["x-dead-letter-routing-key"] = dlqRoutingKey
        };
        await channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false, arguments: queueArgs);
        await channel.QueueBindAsync(queue, exchange, routingKey);

        // 3. Publica mensagem
        var messageBody = Encoding.UTF8.GetBytes("Poison message test");
        await channel.BasicPublishAsync(exchange, routingKey, messageBody);

        // Act: Consome e rejeita a mensagem sem requeue (requeue = false)
        var delivery = await channel.BasicGetAsync(queue, autoAck: false);
        delivery.Should().NotBeNull();
        await channel.BasicRejectAsync(delivery!.DeliveryTag, requeue: false);

        // Assert: A mensagem deve ser redirecionada para a Dead Letter Queue (DLQ)
        var dlqDelivery = await channel.BasicGetAsync(dlq, autoAck: true);
        dlqDelivery.Should().NotBeNull();
        var dlqBody = Encoding.UTF8.GetString(dlqDelivery!.Body.ToArray());
        dlqBody.Should().Be("Poison message test");
    }

    private class TestOptionsMonitor<T> : IOptionsMonitor<T>
    {
        public TestOptionsMonitor(T currentValue) => CurrentValue = currentValue;
        public T CurrentValue { get; }
        public T Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
