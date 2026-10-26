# ADR-006: Observabilidade com OpenTelemetry, Distributed Tracing W3C e Métricas

## Contexto

Em arquiteturas orientadas a eventos onde o fluxo de dados trafega através de pipelines HTTP, bancos relacionais e filas assíncronas no RabbitMQ, o diagnóstico de falhas, latências e anomalias de processamento torna-se complexo sem correlação distribuída de ponta a ponta.

Identificadores isolados por processo impedem que ferramentas de observabilidade (como Jaeger, Prometheus, Grafana Tempo e OpenTelemetry Collector) exibam o grafo completo da transação financeira.

## Decisão Arquitetural

1. **Padrão W3C TraceContext sobre Mensageria AMQP**:
   * Foi criado o componente `RabbitMqTraceContextPropagator` em `CashFlow.Shared.Contracts.Diagnostics`.
   * Durante a publicação (`RabbitMqPublisher.PublishAsync`), o contexto do span ativo (`ActivityKind.Producer`) é injetado nos headers AMQP nos campos padronizados `traceparent` e `tracestate`, e o `CorrelationId` recebe o `TraceId`.
   * Durante o consumo (`TransactionRegisteredConsumer`), o cabeçalho é extraído e uma atividade filha (`ActivityKind.Consumer`) é iniciada sob o mesmo `TraceId` original.

2. **Instrumentação de Métricas de Negócio e Infraestrutura**:
   * Criação de contadores estruturados via `System.Diagnostics.Metrics.Meter`:
     * `cashflow_transactions_registered_total`: Total de lançamentos registrados via API;
     * `cashflow_transactions_published_total`: Total de mensagens de transação publicadas com confirmação;
     * `cashflow_transactions_consumed_total`: Total de mensagens consolidadas com sucesso no saldo diário;
     * `cashflow_transactions_duplicate_total`: Total de mensagens duplicadas interceptadas pela idempotência.

3. **Exportador Padronizado OpenTelemetry Protocol (OTLP)**:
   * Ambas as APIs configuram `builder.Services.AddOpenTelemetry()` com `AddAspNetCoreInstrumentation()` e `AddOtlpExporter()`, permitindo integração direta e agnóstica com qualquer backend de telemetria sem lock-in de fornecedor.

## Consequências

### Positivas
* Visibilidade completa do ciclo de vida da transação da requisição HTTP ao banco de dados e processamento no worker assíncrono;
* Métricas operacionais em tempo real para alertas preditivos e dashboards;
* Conformidade com os padrões da Cloud Native Computing Foundation (CNCF).
