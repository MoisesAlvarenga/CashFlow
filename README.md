# Cash Flow & Daily Consolidation System (Desafio Técnico Verity)

Sistema financeiro distribuído de controle de fluxo de caixa e consolidação diária de lançamentos (créditos e débitos), desenvolvido em **.NET 9 LTS** sob os princípios de **Clean Architecture**, **SOLID**, **Domain-Driven Design (DDD)** e mensageria assíncrona desacoplada com o padrão **Transactional Outbox**.

---

## 🧭 Acesso Rápido às Ferramentas e Relatórios no Navegador

> [!TIP]
> **Como abrir direto no seu Navegador Web**:
> No VS Code ou IDE, clicar em links de arquivos locais (`.html`) abre o código-fonte no editor da IDE por padrão.
> Para abrir o **Archify**, o **Solidify** e o **SonarQube** diretamente no seu navegador padrão (Chrome/Edge/Firefox), execute:
> ```powershell
> powershell -File scripts/open-browser.ps1
> ```

| Ferramenta | O que é | Link de Acesso | Abrir no Navegador |
|---|---|---|---|
| **Archify** | Diagrama interativo de arquitetura de software (fluxos, componentes, resiliência e temas Dark/Light) | [cash-flow.html](.archify/architecture-cash-flow-20261015-133255/cash-flow.html)<br>[candidate.json](.archify/architecture-cash-flow-20261015-133255/candidate.json) | `powershell -File scripts/open-browser.ps1 -Archify` |
| **Solidify** | Avaliador arquitetural SOLID e Quality Gate de release determinístico | [release-report.html](.solidify/out/release-report.html)<br>[release-report.json](.solidify/out/release-report.json) | `powershell -File scripts/open-browser.ps1 -Solidify` |
| **SonarQube** | Dashboard de métricas estáticas, cobertura de testes e Clean Code | [Dashboard SonarQube Local](http://localhost:9000/dashboard?id=desafio_tecnico_Verity) | [Abrir Dashboard SonarQube](http://localhost:9000/dashboard?id=desafio_tecnico_Verity) |
| **Especificações** | Regras de negócio, arquitetura C4 e matriz de testes | [Requisitos de Negócio (Spec)](.specs/features/fluxo-caixa-consolidado/spec.md)<br>[Design Arquitetural](.specs/features/fluxo-caixa-consolidado/design.md)<br>[Tarefas e Test Coverage Matrix](.specs/features/fluxo-caixa-consolidado/tasks.md) | N/A |

---

## 🏛️ Visão Geral da Arquitetura

O sistema adota uma arquitetura de **dois serviços autônomos**, desacoplados física e logicamente por mensageria assíncrona:

```mermaid
flowchart TD
    subgraph Clientes["Clientes & Consumidores"]
        Merchant["Comerciante / Frontend"]
        Relatorios["Sistemas de Relatório / BI"]
    end

    subgraph TransactionsService["Serviço de Lançamentos (CashFlow.Transactions.Api)"]
        TransactionsController["TransactionsController\nPOST /api/v1/transactions"]
        TransactionHandler["RegisterTransactionCommandHandler"]
        TransactionRepo["TransactionRepository"]
        OutboxTable["Tabela Outbox\n(Mesma Transação ACID)"]
        OutboxPublisher["OutboxPublisherWorker\n(BackgroundService)"]
        DbTransactions[("PostgreSQL\nTransactions & Outbox")]
    end

    subgraph Mensageria["Broker de Mensagens"]
        Queue["RabbitMQ / MassTransit\nFila: transaction-registered-event"]
        DLQ["Dead Letter Queue (DLQ)"]
    end

    subgraph ConsolidationService["Serviço de Consolidação (CashFlow.Consolidation.Api)"]
        EventConsumer["TransactionRegisteredConsumer\n(MassTransit Consumer)"]
        DailyBalanceService["DailyBalanceService\n(Agregação Idempotente)"]
        DailyBalanceRepo["DailyBalanceRepository"]
        DbConsolidation[("PostgreSQL\nDaily Balances")]
        DailyBalancesController["DailyBalancesController\nGET /api/v1/daily-balances"]
    end

    Merchant -->|HTTP POST| TransactionsController
    TransactionsController --> TransactionHandler
    TransactionHandler --> TransactionRepo
    TransactionRepo -->|Commit ACID| DbTransactions
    TransactionHandler -->|Grava Evento| OutboxTable
    OutboxTable -.->|Persistido no mesmo DB| DbTransactions

    OutboxPublisher -->|Lê pendentes| OutboxTable
    OutboxPublisher -->|Publica evento| Queue
    Queue -->|Consumo assíncrono| EventConsumer
    Queue -.->|Falhas pós-retries| DLQ
    EventConsumer --> DailyBalanceService
    DailyBalanceService --> DailyBalanceRepo
    DailyBalanceRepo --> DbConsolidation

    Relatorios -->|HTTP GET| DailyBalancesController
    DailyBalancesController --> DailyBalanceRepo
```

### Resiliência e Desacoplamento Garantidos
1. **Padrão Transactional Outbox**: Quando um comerciante registra um crédito ou débito, o lançamento e a mensagem de outbox são salvos na **mesma transação relacional ACID**. O endpoint responde imediatamente com `HTTP 201 Created`.
2. **Independência Operacional**: Se o serviço de consolidação ou o broker RabbitMQ estiverem indisponíveis, **o comerciante continua registrando lançamentos normalmente sem qualquer interrupção**. Assim que o serviço for restabelecido, o `OutboxPublisherWorker` descarrega as mensagens pendentes garantindo consistência eventual sem perda de dados.
3. **Idempotência**: O consumidor de consolidação valida os identificadores na tabela `ProcessedEvents`, assegurando que eventos reenviados ou duplicados não afetem o saldo líquido consolidado.

---

## 🗂️ Estrutura da Solução Clean Architecture

```text
C:\DEV\desafio_tecnico_Verity\
├── .archify/                                  # Diagramas arquiteturais interativos
│   └── architecture-cash-flow-20261015-133255/
│       ├── cash-flow.html                     # Visualizador standalone HTML
│       └── candidate.json                     # Especificação formal Archify
├── .solidify/                                 # Relatórios do Solidify Quality Gate
│   └── out/release-report.json                # Relatório canônico da auditoria
├── .specs/features/fluxo-caixa-consolidado/   # Especificações técnicas
│   ├── spec.md                                # Requisitos e critérios de aceite
│   ├── design.md                              # Decisões de design e C4
│   └── tasks.md                               # Matriz de testes e plano de fases
├── docs/
│   └── SONARQUBE_SOLIDIFY.md                  # Documentação de integração Sonar + Solidify
├── scripts/
│   ├── run-sonar.ps1                          # Automação do scanner SonarQube
│   ├── solidify-audit.ps1                     # Automação do release gate Solidify
│   └── test-runner.cmd                        # Executor determinístico de testes
├── src/
│   ├── Shared/
│   │   └── CashFlow.Shared.Contracts/         # Contratos e eventos compartilhados
│   ├── Transactions/
│   │   ├── CashFlow.Transactions.Domain/      # Entidade Transaction, Enums, Regras puras
│   │   ├── CashFlow.Transactions.Application/ # Handlers CQRS, Validações FluentValidation
│   │   ├── CashFlow.Transactions.Infrastructure/ # EF Core, Outbox Repository, Worker
│   │   └── CashFlow.Transactions.Api/         # REST API de Lançamentos (Porta 5101 / 8080)
│   └── Consolidation/
│       ├── CashFlow.Consolidation.Domain/     # Entidade DailyBalance, Agregação pura
│       ├── CashFlow.Consolidation.Application/# Consumer MassTransit idempotente
│       ├── CashFlow.Consolidation.Infrastructure/ # Persistência de saldos consolidados
│       └── CashFlow.Consolidation.Api/        # REST API de Consulta (Porta 5102 / 8080)
├── tests/
│   ├── CashFlow.UnitTests/                    # Testes unitários de domínio e contratos
│   └── CashFlow.IntegrationTests/             # Testes de integração e cenários de resiliência
├── CashFlow.sln                               # Solução principal .NET 9
├── docker-compose.yml                         # Ecossistema completo (APIs, RabbitMQ, Postgres)
├── docker-compose.sonar.yml                   # Contêiner dedicado SonarQube
└── solidify.json                              # Configuração dos pesos SOLID e Quality Gate
```

---

## 🚀 Como Executar o Projeto

### Pré-requisitos
* [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) instalado
* [Docker Desktop](https://www.docker.com/products/docker-desktop) instalado e em execução

---

### Opção 1: Executando o Ecossistema Completo via Docker Compose

Para subir todos os componentes (APIs, PostgreSQL, RabbitMQ e SonarQube):

```powershell
# Subir todo o ecossistema em segundo plano
docker compose up -d

# Visualizar o status dos contêineres e portas
docker compose ps
```

Portas expostas:
* **API de Lançamentos**: `http://localhost:5101/swagger`
* **API de Consolidação**: `http://localhost:5102/swagger`
* **RabbitMQ Dashboard**: `http://localhost:15672` (Usuário: `verity` / Senha: `verity_secret`)
* **SonarQube Dashboard**: `http://localhost:9000` (Usuário: `admin` / Senha: `admin`)
* **PostgreSQL**: `localhost:5433` (Banco: `cashflow_db`, Usuário: `verity`)

---

### Opção 2: Executando Localmente via CLI do .NET

1. **Restaurar e compilar a solução**:
   ```powershell
   dotnet restore
   dotnet build
   ```

2. **Executar a suíte de testes com cobertura**:
   ```powershell
   dotnet test --logger trx --collect:"XPlat Code Coverage"
   ```

3. **Iniciar a API de Lançamentos**:
   ```powershell
   dotnet run --project src/Transactions/CashFlow.Transactions.Api
   ```

4. **Iniciar a API de Consolidação** (em outro terminal):
   ```powershell
   dotnet run --project src/Consolidation/CashFlow.Consolidation.Api
   ```

---

## 🔍 Qualidade de Código, SonarQube e Solidify

### 1. Executar Análise no SonarQube
O script automatiza o início da sessão com `dotnet-sonarscanner`, compilação, coleta de cobertura OpenCover e submissão dos relatórios ao servidor SonarQube:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/run-sonar.ps1
```
* **Dashboard de Resultados**: [http://localhost:9000/dashboard?id=desafio_tecnico_Verity](http://localhost:9000/dashboard?id=desafio_tecnico_Verity)

### 2. Executar Auditoria do Solidify (Quality Gate & SOLID)
O Solidify inspeciona o histórico de commits do Git, avalia a conformidade arquitetural com os princípios SOLID, lê as métricas publicadas no SonarQube e emite o veredito canônico:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/solidify-audit.ps1 -Profile quick
```
* **Relatório de Saída**: Visualizável em [`.solidify/out/release-report.json`](.solidify/out/release-report.json).

---

## 📡 Exemplos de Chamadas REST (cURL)

### 1. Registrar um Lançamento de Crédito
```bash
curl -X POST "http://localhost:5101/api/v1/transactions" \
     -H "Content-Type: application/json" \
     -d '{
       "type": "Credit",
       "amount": 250.75,
       "date": "2026-10-15",
       "description": "Pagamento de fatura de cliente"
     }'
```
**Resposta esperada (`HTTP 201 Created`)**:
```json
{
  "transactionId": "f47ac10b-58cc-4372-a567-0e02b2c3d479",
  "status": "Registered",
  "registeredAt": "2026-10-15T18:30:00Z"
}
```

### 2. Registrar um Lançamento de Débito
```bash
curl -X POST "http://localhost:5101/api/v1/transactions" \
     -H "Content-Type: application/json" \
     -d '{
       "type": "Debit",
       "amount": 80.00,
       "date": "2026-10-15",
       "description": "Taxa de manutenção operacional"
     }'
```
**Resposta esperada (`HTTP 201 Created`)**:
```json
{
  "transactionId": "3b29f032-1b1d-44a6-981c-91e87d46b2b4",
  "status": "Registered",
  "registeredAt": "2026-10-15T18:32:00Z"
}
```

### 3. Consultar Saldo Diário Consolidado
```bash
curl -X GET "http://localhost:5102/api/v1/daily-balances?date=2026-10-15" \
     -H "Accept: application/json"
```
**Resposta esperada (`HTTP 200 OK`)**:
```json
{
  "date": "2026-10-15",
  "totalCredits": 250.75,
  "totalDebits": 80.00,
  "netBalance": 170.75,
  "transactionCount": 2,
  "updatedAt": "2026-10-15T18:32:05Z"
}
```

---

## 🛡️ Princípios SOLID Aplicados

* **S (Single Responsibility Principle)**: Entidades de domínio focadas em invariantes financeiras; handlers de comando dedicados a uma única operação; worker responsável apenas por publicar mensagens do Outbox.
* **O (Open-Closed Principle)**: Novos tipos de transações e novos consumidores podem ser adicionados via mensageria sem modificar classes ou fluxos existentes.
* **L (Liskov Substitution Principle)**: Contratos de repositório e abstrações do MassTransit (`IPublishEndpoint`, `IConsumer`) respeitam estritamente seus contratos sem comportamentos inesperados.
* **I (Interface Segregation Principle)**: Interfaces enxutas e focadas (`ITransactionRepository`, `IOutboxRepository`, `IDailyBalanceRepository`).
* **D (Dependency Inversion Principle)**: Todas as camadas de Domínio e Aplicação dependem de abstrações (interfaces); infraestrutura (banco de dados, RabbitMQ, EF Core) depende do núcleo.

---

## 📄 Licença e Conformidade

Projeto desenvolvido como solução do desafio técnico para a Verity. Todas as decisões técnicas e contratos foram validados e homologados pelos relatórios automatizados de conformidade.
