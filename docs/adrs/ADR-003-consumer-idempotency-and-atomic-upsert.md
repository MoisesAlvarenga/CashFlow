# ADR-003: Idempotência do Consumidor e Upsert Atômico no Saldo Diário

## Contexto

Em arquiteturas assíncronas orientadas a eventos com garantia de entrega *At-Least-Once*, mensagens podem ser entregues em duplicidade devido a quedas temporárias de rede antes do envio do ACK pelo consumidor ou retentativas automáticas no broker.

No microsserviço de Consolidação, processar um evento de R$ 1.000,00 duas vezes causaria distorção catastrófica no saldo diário (*double accounting*). Além disso, múltiplas mensagens referentes ao mesmo dia contábil podem ser processadas simultaneamente por threads ou instâncias concorrentes, expondo o sistema a *lost updates* caso o cálculo seja realizado via padrão tradicional de leitura-modificação-escrita (*read-modify-write* em memória).

## Decisão Arquitetural

1. **Deduplicação de Mensagens via Chave Primária em Banco Relacional**:
   * Foi criada a tabela `ConsumedMessages` com chave primária natural no identificador da transação (`PK_ConsumedMessages` em `TransactionId`).
   * No `DailyBalanceService.ProcessTransactionAsync`, antes de qualquer cálculo, verifica-se a existência da mensagem na tabela.
   * Se já existir, a mensagem é ignorada e confirmada (`BasicAckAsync`), incrementando o contador de duplicatas com segurança.

2. **Atomic SQL Upsert no PostgreSQL**:
   * A consolidação diária dispensa a leitura da linha para a memória da aplicação. A atualização é executada em uma única instrução nativa do PostgreSQL:
     ```sql
     INSERT INTO "DailyBalances" ("Date", "TotalCredits", "TotalDebits", "NetBalance", "TransactionCount", "LastUpdatedAt")
     VALUES (@Date, @Credit, @Debit, @Net, 1, @Now)
     ON CONFLICT ("Date") DO UPDATE
     SET "TotalCredits" = "DailyBalances"."TotalCredits" + EXCLUDED."TotalCredits",
         "TotalDebits" = "DailyBalances"."TotalDebits" + EXCLUDED."TotalDebits",
         "NetBalance" = "DailyBalances"."NetBalance" + EXCLUDED."NetBalance",
         "TransactionCount" = "DailyBalances"."TransactionCount" + 1,
         "LastUpdatedAt" = EXCLUDED."LastUpdatedAt";
     ```
   * O bloqueio por linha (*row-level lock*) é gerenciado nativamente pelo mecanismo MVCC do PostgreSQL na chave primária `Date`.

3. **Atomicidade da Consolidação e Deduplicação**:
   * O upsert do saldo diário e o registro em `ConsumedMessages` são executados dentro da mesma transação física (`IConsolidationUnitOfWork.ExecuteInTransactionAsync`), garantindo que ambos persistam juntos ou nenhum efeito colateral permaneça.

## Consequências

### Positivas
* **Imunidade a Duplicatas**: Garantia matemática de que cada evento de transação afeta o saldo exatamente uma vez (*effectively once*);
* **Zero Atualizações Perdidas (*Zero Lost Updates*)**: Testado sob concorrência extrema (30 tasks concorrentes na mesma data contábil) com precisão centesimal sem deadlocks;
* **Altíssima Performance**: Eliminação do overhead de round-trips de leitura prévia e locks pessimistas em nível de aplicação.
