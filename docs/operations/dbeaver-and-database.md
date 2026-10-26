# Guia do Banco de Dados PostgreSQL e DBeaver

## 1. Dados de Conexão (PostgreSQL)

Quando o ambiente é inicializado via Docker Compose (`docker compose up -d`), o contêiner do PostgreSQL fica disponível nas seguintes credenciais:

| Parâmetro | Valor Padrão |
|---|---|
| **Host** | `localhost` |
| **Porta** | `5433` (mapeada da porta interna 5432) |
| **Database** | `cashflow_db` |
| **Username** | `verity` |
| **Password** | `verity_secret` |
| **Schema** | `public` |

---

## 2. Tabelas Criadas Automaticamente por Migrations

No momento em que os microsserviços sobem, o Entity Framework Core aplica automaticamente as migrations Code-First:

1. **`Transactions`**: Armazena as movimentações financeiras individuais registradas pela API de Lançamentos.
2. **`OutboxMessages`**: Fila persistente de eventos pendentes de publicação no RabbitMQ.
3. **`IdempotencyRecords`**: Registros de deduplicação das chamadas HTTP via `Idempotency-Key`.
4. **`DailyBalances`**: Tabela agregadora de saldos diários com chave primária em `Date`.
5. **`ConsumedMessages`**: Registro de deduplicação das mensagens consumidas pelo worker de consolidação.
6. **`__EFMigrationsHistory_Transactions`** e **`__EFMigrationsHistory_Consolidation`**: Histórico versionado de migrations do EF Core.

---

## 3. Consultas Úteis para Auditoria e Validação

### Verificar Lançamentos e Status do Outbox
```sql
SELECT "Id", "Type", "Amount", "Date", "Description", "CreatedAt" 
FROM "Transactions" 
ORDER BY "CreatedAt" DESC;

SELECT "Id", "EventType", "RetryCount", "ProcessedAt", "CreatedAt" 
FROM "OutboxMessages" 
ORDER BY "CreatedAt" DESC;
```

### Consultar Saldos Consolidados e Mensagens Consumidas
```sql
SELECT "Date", "TotalCredits", "TotalDebits", "NetBalance", "TransactionCount", "LastUpdatedAt" 
FROM "DailyBalances" 
ORDER BY "Date" DESC;

SELECT "TransactionId", "ConsumedAt" 
FROM "ConsumedMessages" 
ORDER BY "ConsumedAt" DESC;
```
