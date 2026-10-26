namespace CashFlow.Consolidation.Api.Workers;

using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using CashFlow.Consolidation.Application.Services;
using CashFlow.Shared.Contracts.Events;
using CashFlow.Shared.Contracts.Messaging;
using Microsoft.Extensions.Options;
using Npgsql;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

/// <summary>
/// Consumidor em segundo plano (BackgroundService) que escuta a fila do RabbitMQ
/// e consolida o saldo diário de forma assíncrona para cada transação recebida.
/// Possui topologia com Dead Letter Queue (DLQ) e política de retry para falhas transitórias.
/// </summary>
public class TransactionRegisteredConsumer : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<RabbitMqOptions> _optionsMonitor;
    private readonly ILogger<TransactionRegisteredConsumer> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public TransactionRegisteredConsumer(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<RabbitMqOptions> optionsMonitor,
        ILogger<TransactionRegisteredConsumer> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _optionsMonitor = optionsMonitor ?? throw new ArgumentNullException(nameof(optionsMonitor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Iniciando TransactionRegisteredConsumer...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var options = _optionsMonitor.CurrentValue;
                var factory = new ConnectionFactory
                {
                    HostName = options.Host,
                    Port = options.Port,
                    UserName = options.Username,
                    Password = options.Password
                };

                await using var connection = await factory.CreateConnectionAsync(stoppingToken);
                await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

                await EnsureTopologyAsync(connection, channel, options, stoppingToken);

                // Configura prefetch para 10 mensagens por vez (Load Balancing)
                await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 10, global: false, cancellationToken: stoppingToken);

                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += async (sender, ea) =>
                {
                    var deliveryTag = ea.DeliveryTag;
                    try
                    {
                        var body = ea.Body.ToArray();
                        var json = Encoding.UTF8.GetString(body);
                        var evt = JsonSerializer.Deserialize<TransactionRegisteredEvent>(json, JsonOptions);

                        if (evt == null)
                        {
                            _logger.LogWarning("Mensagem com payload vazio ou nulo recebida. Encaminhando para DLQ.");
                            await channel.BasicNackAsync(deliveryTag, multiple: false, requeue: false, cancellationToken: stoppingToken);
                            return;
                        }

                        var maxAttempts = options.MaxRetryAttempts;
                        var attempt = 0;
                        var applied = false;

                        while (true)
                        {
                            attempt++;
                            try
                            {
                                using var scope = _scopeFactory.CreateScope();
                                var balanceService = scope.ServiceProvider.GetRequiredService<IDailyBalanceService>();

                                applied = await balanceService.ProcessTransactionAsync(
                                    evt.TransactionId,
                                    evt.Type,
                                    evt.Amount,
                                    evt.Date,
                                    stoppingToken);

                                break;
                            }
                            catch (Exception ex) when (IsTransientException(ex) && attempt < maxAttempts)
                            {
                                var delayMs = (int)(Math.Pow(2, attempt) * 200);
                                _logger.LogWarning(ex,
                                    "Falha transitória ao processar transação {TransactionId} (tentativa {Attempt}/{Max}). Nova tentativa em {Delay}ms...",
                                    evt.TransactionId, attempt, maxAttempts, delayMs);
                                await Task.Delay(delayMs, stoppingToken);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex,
                                    "Falha definitiva ao processar transação {TransactionId} após {Attempt} tentativa(s). Encaminhando mensagem para a DLQ ({DlqQueue}).",
                                    evt.TransactionId, attempt, options.DeadLetterQueueName);

                                await channel.BasicNackAsync(deliveryTag, multiple: false, requeue: false, cancellationToken: stoppingToken);
                                return;
                            }
                        }

                        if (applied)
                        {
                            _logger.LogInformation(
                                "RabbitMQ: Evento de transação {TransactionId} ({Type} de R$ {Amount}) consumido e consolidado para {Date}.",
                                evt.TransactionId, evt.Type, evt.Amount, evt.Date);
                        }
                        else
                        {
                            _logger.LogInformation(
                                "RabbitMQ: Evento da transação {TransactionId} já consolidado; ignorando duplicata.",
                                evt.TransactionId);
                        }

                        await channel.BasicAckAsync(deliveryTag, multiple: false, cancellationToken: stoppingToken);
                    }
                    catch (JsonException jsonEx)
                    {
                        _logger.LogError(jsonEx, "Erro irrecuperável de desserialização JSON (Poison Message). Encaminhando para DLQ.");
                        await channel.BasicNackAsync(deliveryTag, multiple: false, requeue: false, cancellationToken: stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Erro inesperado ao consumir mensagem do RabbitMQ. Encaminhando para DLQ.");
                        await channel.BasicNackAsync(deliveryTag, multiple: false, requeue: false, cancellationToken: stoppingToken);
                    }
                };

                await channel.BasicConsumeAsync(
                    queue: options.QueueName,
                    autoAck: false,
                    consumer: consumer,
                    cancellationToken: stoppingToken);

                _logger.LogInformation("TransactionRegisteredConsumer conectado e escutando a fila '{Queue}' (DLQ: '{DlqQueue}').",
                    options.QueueName, options.DeadLetterQueueName);

                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro de conexão no TransactionRegisteredConsumer. Tentando reconectar em 5 segundos...");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        _logger.LogInformation("TransactionRegisteredConsumer finalizado.");
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

    public static async Task DeclareTopologyAsync(
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

        // 5. Main Queue with DLX configuration
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

    public static bool IsTransientException(Exception ex)
    {
        if (ex is ArgumentException or JsonException or FormatException)
        {
            return false;
        }

        if (ex is TimeoutException or SocketException)
        {
            return true;
        }

        if (ex is NpgsqlException npgsqlEx && npgsqlEx.IsTransient)
        {
            return true;
        }

        if (ex.InnerException != null)
        {
            return IsTransientException(ex.InnerException);
        }

        return false;
    }
}
