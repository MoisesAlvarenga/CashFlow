namespace CashFlow.Transactions.Api.Workers;

using System.Text.Json;
using CashFlow.Shared.Contracts.Events;
using CashFlow.Transactions.Api.Messaging;
using CashFlow.Transactions.Domain.Repositories;
using Microsoft.Extensions.Options;

/// <summary>
/// Serviço de segundo plano (BackgroundService) responsável pelo despacho assíncrono
/// das mensagens registradas na tabela Outbox para o Message Broker RabbitMQ.
/// Garante entrega no padrão At-Least-Once com intervalo de execução configurável.
/// </summary>
public class OutboxProcessorWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IRabbitMqPublisher _rabbitMqPublisher;
    private readonly IOptionsMonitor<OutboxProcessorOptions> _optionsMonitor;
    private readonly ILogger<OutboxProcessorWorker> _logger;

    public OutboxProcessorWorker(
        IServiceScopeFactory scopeFactory,
        IRabbitMqPublisher rabbitMqPublisher,
        IOptionsMonitor<OutboxProcessorOptions> optionsMonitor,
        ILogger<OutboxProcessorWorker> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _rabbitMqPublisher = rabbitMqPublisher ?? throw new ArgumentNullException(nameof(rabbitMqPublisher));
        _optionsMonitor = optionsMonitor ?? throw new ArgumentNullException(nameof(optionsMonitor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "OutboxProcessorWorker inicializado com RabbitMQ. Intervalo configurado: {Interval}s | Tamanho de lote: {BatchSize}.",
            _optionsMonitor.CurrentValue.IntervalSeconds,
            _optionsMonitor.CurrentValue.BatchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingMessagesAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Erro inesperado durante o processamento do lote da Outbox.");
            }

            var delaySeconds = Math.Max(1, _optionsMonitor.CurrentValue.IntervalSeconds);
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), stoppingToken);
        }

        _logger.LogInformation("OutboxProcessorWorker finalizado.");
    }

    private async Task ProcessPendingMessagesAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var outboxRepository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();

        var batchSize = Math.Max(1, _optionsMonitor.CurrentValue.BatchSize);
        var pendingMessages = await outboxRepository.GetPendingMessagesAsync(batchSize);

        if (pendingMessages.Count == 0)
        {
            return;
        }

        _logger.LogInformation("OutboxProcessorWorker: {Count} mensagem(ns) pendente(s) identificada(s) para envio ao RabbitMQ.", pendingMessages.Count);

        foreach (var message in pendingMessages)
        {
            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                if (message.EventType == nameof(TransactionRegisteredEvent))
                {
                    // Publica o evento no RabbitMQ
                    await _rabbitMqPublisher.PublishAsync(message.Payload, stoppingToken);

                    // Confirma processamento na tabela de Outbox
                    await outboxRepository.MarkAsProcessedAsync(message.Id);

                    _logger.LogInformation("Mensagem Outbox {Id} publicada com sucesso no RabbitMQ e marcada como processada.", message.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao publicar a mensagem Outbox {Id} no RabbitMQ. Será tentada novamente no próximo ciclo.", message.Id);
            }
        }
    }
}
