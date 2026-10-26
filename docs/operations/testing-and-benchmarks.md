# Testes Automatizados, Performance e Quality Gates

## 1. Estratégia de Testes da Solução

A pirâmide de testes do **CashFlow** garante cobertura estrita em três níveis fundamentais:
1. **Testes Unitários**: Validação determinística de regras de domínio, handlers de comando/consulta, validações e propagação de telemetria sem dependência de I/O de rede.
2. **Testes de Integração com Contêineres Reais (Testcontainers)**: Validação de transações ACID no PostgreSQL real, Publisher Confirms e Dead Letter Queue no RabbitMQ real.
3. **Testes de Carga e Estresse (Grafana K6)**: Validação do SLA não funcional de suportar mais de 50 req/s com p95 < 500ms e perda máxima de 5%.

---

## 2. Suíte de Testes Unitários (`CashFlow.UnitTests`)

A suíte conta com **74 testes unitários** automatizados com tempo de execução de ~230ms:

| Arquivo de Teste | Camada / Módulo | Total | Foco de Cobertura |
|---|---|:---:|---|
| [`TransactionTests.cs`](../../tests/CashFlow.UnitTests/Transactions/Domain/TransactionTests.cs) | Domínio (Lançamentos) | 7 | Criação de crédito/débito, rejeição de valor zero/negativo e sanitização de descrição. |
| [`DailyBalanceTests.cs`](../../tests/CashFlow.UnitTests/Consolidation/Domain/DailyBalanceTests.cs) | Domínio (Consolidação) | 8 | Aritmética de consolidação, saldo líquido (`Credits - Debits`), contadores e saldos negativos. |
| [`OutboxMessageTests.cs`](../../tests/CashFlow.UnitTests/Transactions/Domain/OutboxMessageTests.cs) | Domínio (Outbox) | 2 | Estado inicial não processado, mutabilidade e contadores de retentativa. |
| [`RegisterTransactionCommandValidatorTests.cs`](../../tests/CashFlow.UnitTests/Transactions/Application/RegisterTransactionCommandValidatorTests.cs) | Validações Fluent | 15 | Regras de validação de valores monetários, descrições e enums. |
| [`RegisterTransactionCommandHandlerTests.cs`](../../tests/CashFlow.UnitTests/Transactions/Application/RegisterTransactionCommandHandlerTests.cs) | CQRS Comandos | 8 | Persistência atômica com Outbox, deduplicação por `Idempotency-Key` e tratamento de concorrência. |
| [`GetTransactionsQueryHandlerTests.cs`](../../tests/CashFlow.UnitTests/Transactions/Application/GetTransactionsQueryHandlerTests.cs) | CQRS Consultas | 7 | Filtros de data, segmentação de páginas com `PagedResult<T>` e limites de contorno. |
| [`DailyBalanceServiceTests.cs`](../../tests/CashFlow.UnitTests/Consolidation/Application/DailyBalanceServiceTests.cs) | Consolidação | 5 | Processamento incremental atômico, retorno de 404 para datas vazias e projeção DTO. |
| [`RabbitMqTraceContextPropagatorTests.cs`](../../tests/CashFlow.UnitTests/Shared/RabbitMqTraceContextPropagatorTests.cs) | Telemetria OpenTelemetry | 6 | Injeção e extração de W3C TraceContext (`traceparent`/`tracestate`) sobre AMQP. |
| [`JwtTokenServiceTests.cs`](../../tests/CashFlow.UnitTests/Shared/JwtTokenServiceTests.cs) | Segurança JWT | 6 | Assinatura HMAC-SHA256, geração de claims (`sub`, `jti`, `role`) e expiração. |
| [`TransactionRegisteredEventTests.cs`](../../tests/CashFlow.UnitTests/Shared/TransactionRegisteredEventTests.cs) | Mensageria | 2 | Serialização/desserialização JSON sem truncamento de tipos (`DateOnly`, `decimal`, `Guid`). |
| [`RabbitMqOptionsTests.cs`](../../tests/CashFlow.UnitTests/Shared/RabbitMqOptionsTests.cs) | Configuração | 8 | Validação de opções e argumentos de conexão do broker. |

---

## 3. Testes de Integração com Contêineres Reais (`CashFlow.IntegrationTests`)

Os testes de integração utilizam **Testcontainers.PostgreSql** e **Testcontainers.RabbitMq**:
* **`TransactionsDualWriteIntegrationTests.cs`**: Valida a persistência atômica entre transação, outbox e chave de idempotência no banco de dados real.
* **`ConsolidationConsumerIntegrationTests.cs`**: Valida a deduplicação de mensagens idênticas via constraint física da tabela `ConsumedMessages`.
* **`DailyBalanceConcurrencyIntegrationTests.cs`**: Executa 30 tasks paralelas de crédito e débito para o mesmo dia contábil, comprovando **zero perda de atualização (*Zero Lost Updates*)**.
* **`RabbitMqEndToEndIntegrationTests.cs`**: Valida o pipeline de entrega com Publisher Confirms e o redirecionamento automático de mensagens venenosas para a Dead Letter Queue (DLQ).

---

## 4. Testes de Carga e Estresse (Grafana K6)

Executados sob pico de carga superior a 50 requisições simultâneas por segundo:

| Script K6 | Tipo | Cenário de Carga | SLA Aceito | Resultado Medido | Status |
|---|:---:|:---:|:---:|:---:|:---:|
| `k6-smoke-test.js` | Smoke Test | 2 VUs contínuas por 10s | p95 < 500ms, Erro < 1% | **p95: 15.20ms, 0% de erro** | ✅ Aprovado |
| `k6-load-test.js` | Carga Contínua | 10 VUs (~50+ req/s por 30s) | p95 < 500ms, Erro < 5% | **p95: 40.63ms, 0% de erro** | ✅ Aprovado |

---

## 5. Como Executar os Testes

```powershell
# Executar todos os testes unitários via CLI
dotnet test tests/CashFlow.UnitTests

# Executar testes unitários com script de automação
powershell -File scripts/run-tests.ps1

# Executar testes de carga K6
k6 run tests/load/k6-load-test.js
```
