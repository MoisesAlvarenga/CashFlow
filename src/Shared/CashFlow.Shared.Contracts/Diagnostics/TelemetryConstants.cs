namespace CashFlow.Shared.Contracts.Diagnostics;

using System.Diagnostics;
using System.Diagnostics.Metrics;

/// <summary>
/// Constantes, ActivitySources e Meters compartilhados para observabilidade padronizada com OpenTelemetry.
/// </summary>
public static class TelemetryConstants
{
    public const string TransactionsServiceName = "CashFlow.Transactions";
    public const string ConsolidationServiceName = "CashFlow.Consolidation";
    public const string ServiceVersion = "1.0.0";

    // ActivitySources para Distributed Tracing
    public static readonly ActivitySource TransactionsActivitySource = new(TransactionsServiceName, ServiceVersion);
    public static readonly ActivitySource ConsolidationActivitySource = new(ConsolidationServiceName, ServiceVersion);

    // Meters para Métricas Operacionais
    public static readonly Meter TransactionsMeter = new(TransactionsServiceName, ServiceVersion);
    public static readonly Meter ConsolidationMeter = new(ConsolidationServiceName, ServiceVersion);

    // Contadores de Métricas de Negócio e Mensageria
    public static readonly Counter<long> TransactionsRegisteredCounter =
        TransactionsMeter.CreateCounter<long>(
            name: "cashflow_transactions_registered_total",
            description: "Total acumulado de transações financeiras registradas via API");

    public static readonly Counter<long> TransactionsPublishedCounter =
        TransactionsMeter.CreateCounter<long>(
            name: "cashflow_transactions_published_total",
            description: "Total acumulado de mensagens de transação publicadas com sucesso no RabbitMQ");

    public static readonly Counter<long> TransactionsConsumedCounter =
        ConsolidationMeter.CreateCounter<long>(
            name: "cashflow_transactions_consumed_total",
            description: "Total acumulado de eventos de transação consolidados no saldo diário");

    public static readonly Counter<long> TransactionsDuplicateCounter =
        ConsolidationMeter.CreateCounter<long>(
            name: "cashflow_transactions_duplicate_total",
            description: "Total acumulado de eventos duplicados descartados com segurança por idempotência");
}
