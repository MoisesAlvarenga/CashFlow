# ADR-002: Topologia RabbitMQ com Publisher Confirms, Conexão Persistente e Dead Letter Queue (DLQ)

## Contexto

A mensageria entre os microsserviços de Lançamentos (`CashFlow.Transactions`) e Consolidação (`CashFlow.Consolidation`) exige garantias formais contra:
1. Perda de mensagens durante o envio para o broker;
2. Sobrecarga de abertura excessiva de conexões TCP/AMQP;
3. Travamento da fila por mensagens venenosas (*poison messages*) que geram erros irrecuperáveis e entram em loop infinito de rejeição (`requeue: true`).

## Decisão Arquitetural

1. **Publisher Confirms e Conexão Singleton**:
   * O `RabbitMqPublisher` mantém uma única instância persistente de `IConnection` gerenciada com `SemaphoreSlim` thread-safe e auto-recuperação (`AutomaticRecoveryEnabled = true`).
   * As publicações são executadas com confirmação do broker ativada (`publisherConfirmationsEnabled: true`, `mandatory: true` e `DeliveryModes.Persistent`), garantindo persistência em disco no RabbitMQ antes de liberar a thread.
   * Política de resiliência via **Polly** com 3 retentativas e backoff exponencial progressivo (400ms, 800ms, 1600ms) para falhas transitórias de conexão (`BrokerUnreachableException`, `SocketException`, etc.).

2. **Topologia com Dead Letter Queue (DLQ)**:
   * **Exchange Principal**: `cashflow.transactions` (Direct Exchange).
   * **Fila Principal**: `cashflow.consolidation.daily-balances`, configurada com os argumentos:
     * `x-dead-letter-exchange`: `cashflow.transactions.dlx`
     * `x-dead-letter-routing-key`: `cashflow.consolidation.daily-balances.dead-letter`
   * **Dead Letter Exchange (DLX)**: `cashflow.transactions.dlx` (Direct Exchange).
   * **Dead Letter Queue (DLQ)**: `cashflow.consolidation.daily-balances.dlq`.

3. **Classificação de Falhas no Consumidor**:
   * **Erros Transitórios** (timeouts de banco, quedas momentâneas de socket): até 3 retentativas locais com backoff exponencial;
   * **Erros Definitivos / Mensagens Venenosas** (`JsonException`, payload inválido/nulo ou esgotamento de retentativas): rejeição com `BasicNackAsync(requeue: false)`, despachando a mensagem imediatamente para a DLQ para isolamento e análise forense, desobstruindo o fluxo contábil.

## Consequências

### Positivas
* Zero perda de dados na publicação com Publisher Confirms;
* Alta performance e estabilidade de recursos com reutilização de conexão TCP;
* Proteção contra saturação de CPU gerada por loops infinitos de redelivery.
