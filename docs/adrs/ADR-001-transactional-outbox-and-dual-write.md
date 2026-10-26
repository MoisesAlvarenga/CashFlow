# ADR-001: Padrão Transactional Outbox e Prevenção do Problema Dual-Write

## Contexto

Em sistemas distribuídos financeiros, um dos maiores desafios de confiabilidade é a gravação síncrona simultânea em um banco de dados relacional e a publicação de eventos em um broker de mensageria (*Dual-Write Problem*).

Se o sistema salvar a transação no banco de dados e a rede cair antes de publicar no RabbitMQ, o saldo diário nunca será consolidado (perda de consistência eventual). Por outro lado, se a mensagem for publicada antes do commit no banco de dados e o commit falhar, o saldo será consolidado com dados de uma transação inexistente (*Ghost Write*).

## Decisão Arquitetural

Adotar o padrão **Transactional Outbox** com persistência atômica via `ITransactionsUnitOfWork`:
1. Quando uma requisição de lançamento é recebida em `POST /api/v1/transactions`, o registro contábil `Transaction`, a chave de idempotência `IdempotencyRecord` e a mensagem de integração `OutboxMessage` são incluídos no mesmo `DbContext` do Entity Framework Core.
2. A gravação é confirmada de forma atômica no PostgreSQL dentro de uma única transação física ACID (`BEGIN ... COMMIT`).
3. Um serviço em segundo plano (`OutboxProcessorWorker`) varre periodicamente as mensagens pendentes (`ProcessedAt IS NULL`) e despacha os eventos para o broker RabbitMQ utilizando `RabbitMqPublisher`.
4. Após confirmação do broker (*Publisher Confirms*), a mensagem de Outbox é marcada com o timestamp de processamento (`ProcessedAt = DateTimeOffset.UtcNow`).

## Consequências

### Positivas
* **Eliminação total do Dual-Write**: A transação de negócio e a intenção de publicação são indivisíveis;
* **Resiliência a indisponibilidades do RabbitMQ**: Se o broker estiver inoperante, as transações continuam sendo registradas com sucesso e os eventos permanecem seguros na tabela `OutboxMessages` para envio assim que a conectividade for restabelecida;
* **Garantia *At-Least-Once***: Toda mensagem persistida no Outbox será entregue ao broker pelo menos uma vez.

### Desafios / Mitigações
* Possibilidade de reenvio de mensagens em caso de falha após a publicação e antes do update do `ProcessedAt`. Mitigado pela **idempotência estrita no consumidor** via `ConsumedMessages` (ver [ADR-003](ADR-003-consumer-idempotency-and-atomic-upsert.md)).
