namespace CashFlow.Consolidation.Api.Workers;

using System.Text;
using System.Text.Json;
using CashFlow.Consolidation.Application.Services;
using CashFlow.Shared.Contracts.Events;
using CashFlow.Shared.Contracts.Messaging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

/// <summary>
/// Consumidor em segundo plano (BackgroundService) que escuta a fila do RabbitMQ
/// e consolida o saldo diário de forma assíncrona para cada transação recebida.
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

                await channel.ExchangeDeclareAsync(
                    exchange: options.ExchangeName,
                    type: ExchangeType.Direct,
                    durable: true,
                    autoDelete: false,
                    cancellationToken: stoppingToken);

                await channel.QueueDeclareAsync(
                    queue: options.QueueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null,
                    cancellationToken: stoppingToken);

                await channel.QueueBindAsync(
                    queue: options.QueueName,
                    exchange: options.ExchangeName,
                    routingKey: options.RoutingKey,
                    cancellationToken: stoppingToken);

                // Configura prefetch para 10 mensagens por vez (Load Balancing)
                await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 10, global: false, cancellationToken: stoppingToken);

                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += async (sender, ea) =>
                {
                    try
                    {
                        var body = ea.Body.ToArray();
                        var json = Encoding.UTF8.GetString(body);
                        var evt = JsonSerializer.Deserialize<TransactionRegisteredEvent>(json, JsonOptions);

                        if (evt != null)
                        {
                            using var scope = _scopeFactory.CreateScope();
                            var balanceService = scope.ServiceProvider.GetRequiredService<IDailyBalanceService>();

                            var applied = await balanceService.ProcessTransactionAsync(
                                evt.TransactionId,
                                evt.Type,
                                evt.Amount,
                                evt.Date,
                                stoppingToken);

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
                        }

                        await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Erro ao processar mensagem recebida do RabbitMQ.");
                        // Rejeita e devolve para a fila caso falhe (ou DLQ)
                        await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true, cancellationToken: stoppingToken);
                    }
                };

                await channel.BasicConsumeAsync(
                    queue: options.QueueName,
                    autoAck: false,
                    consumer: consumer,
                    cancellationToken: stoppingToken);

                _logger.LogInformation("TransactionRegisteredConsumer conectado e escutando a fila '{Queue}'.", options.QueueName);

                // Mantém a conexão ativa enquanto não houver cancelamento
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
}
