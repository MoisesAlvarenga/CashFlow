namespace CashFlow.Transactions.Api.Messaging;

using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using CashFlow.Shared.Contracts.Diagnostics;
using CashFlow.Shared.Contracts.Messaging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

public interface IRabbitMqPublisher
{
    Task PublishAsync(string payload, CancellationToken cancellationToken = default);
}

public class RabbitMqPublisher : IRabbitMqPublisher, IAsyncDisposable
{
    private readonly IOptionsMonitor<RabbitMqOptions> _optionsMonitor;
    private readonly ILogger<RabbitMqPublisher> _logger;
    private readonly AsyncRetryPolicy _retryPolicy;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private IConnection? _connection;
    private bool _disposed;

    public RabbitMqPublisher(
        IOptionsMonitor<RabbitMqOptions> optionsMonitor,
        ILogger<RabbitMqPublisher> logger)
    {
        _optionsMonitor = optionsMonitor ?? throw new ArgumentNullException(nameof(optionsMonitor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Política de resiliência via Polly: 3 retentativas com backoff exponencial para falhas transitórias
        _retryPolicy = Policy
            .Handle<BrokerUnreachableException>()
            .Or<SocketException>()
            .Or<TimeoutException>()
            .Or<AlreadyClosedException>()
            .Or<ConnectFailureException>()
            .Or<PublishException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt)), // 400ms, 800ms, 1600ms
                onRetry: (exception, timeSpan, retryCount, _) =>
                {
                    _logger.LogWarning(exception,
                        "Tentativa {RetryCount} de publicação no RabbitMQ falhou. Nova tentativa em {Delay}ms...",
                        retryCount, timeSpan.TotalMilliseconds);
                });
    }

    public async Task PublishAsync(string payload, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var options = _optionsMonitor.CurrentValue;

        await _retryPolicy.ExecuteAsync(async () =>
        {
            var connection = await GetOrCreateConnectionAsync(options, cancellationToken);

            // Cria canal com confirmação de publicação habilitada (Publisher Confirms)
            var channelOptions = new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true);

            await using var channel = await connection.CreateChannelAsync(channelOptions, cancellationToken);

            await EnsureTopologyAsync(connection, channel, options, cancellationToken);

            using var activity = TelemetryConstants.TransactionsActivitySource.StartActivity(
                "RabbitMQ.Publish",
                ActivityKind.Producer);

            activity?.SetTag("messaging.system", "rabbitmq");
            activity?.SetTag("messaging.destination", options.ExchangeName);
            activity?.SetTag("messaging.destination_kind", "exchange");
            activity?.SetTag("messaging.rabbitmq.routing_key", options.RoutingKey);

            var body = Encoding.UTF8.GetBytes(payload);
            var properties = new BasicProperties
            {
                DeliveryMode = DeliveryModes.Persistent,
                ContentType = "application/json",
                Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
                Headers = new Dictionary<string, object?>()
            };

            // Injeta o W3C TraceContext (traceparent/tracestate) para rastreabilidade distribuída
            RabbitMqTraceContextPropagator.InjectTraceContext(properties.Headers, activity);

            if (activity != null)
            {
                properties.CorrelationId = activity.TraceId.ToString();
            }

            await channel.BasicPublishAsync(
                exchange: options.ExchangeName,
                routingKey: options.RoutingKey,
                mandatory: true,
                basicProperties: properties,
                body: body,
                cancellationToken: cancellationToken);

            TelemetryConstants.TransactionsPublishedCounter.Add(1);

            _logger.LogInformation(
                "RabbitMQ: Mensagem publicada com sucesso com Publisher Confirms (Exchange: {Exchange}, RoutingKey: {RoutingKey}).",
                options.ExchangeName, options.RoutingKey);
        });
    }

    private async Task<IConnection> GetOrCreateConnectionAsync(RabbitMqOptions options, CancellationToken cancellationToken)
    {
        if (_connection is { IsOpen: true })
        {
            return _connection;
        }

        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (_connection is { IsOpen: true })
            {
                return _connection;
            }

            if (_connection != null)
            {
                await _connection.DisposeAsync();
                _connection = null;
            }

            var factory = new ConnectionFactory
            {
                HostName = options.Host,
                Port = options.Port,
                UserName = options.Username,
                Password = options.Password,
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(5)
            };

            _logger.LogInformation("Estabelecendo conexão persistente (Singleton) com o RabbitMQ em {Host}:{Port}...",
                options.Host, options.Port);

            _connection = await factory.CreateConnectionAsync(cancellationToken);
            return _connection;
        }
        finally
        {
            _connectionLock.Release();
        }
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

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        if (_connection != null)
        {
            try
            {
                await _connection.DisposeAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Erro ao encerrar a conexão do RabbitMQ.");
            }
        }

        _connectionLock.Dispose();
        GC.SuppressFinalize(this);
    }
}
