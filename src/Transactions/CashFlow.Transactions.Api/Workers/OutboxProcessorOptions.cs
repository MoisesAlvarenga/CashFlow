namespace CashFlow.Transactions.Api.Workers;

/// <summary>
/// Opções de configuração para o worker de processamento da tabela Outbox.
/// Carregadas a partir do appsettings.json ou variáveis de ambiente.
/// </summary>
public class OutboxProcessorOptions
{
    public const string SectionName = "OutboxProcessor";

    /// <summary>
    /// Intervalo em segundos entre cada ciclo de execução do worker. Padrão: 5 segundos.
    /// </summary>
    public int IntervalSeconds { get; set; } = 5;

    /// <summary>
    /// Quantidade máxima de mensagens pendentes a serem processadas por ciclo. Padrão: 20.
    /// </summary>
    public int BatchSize { get; set; } = 20;
}
