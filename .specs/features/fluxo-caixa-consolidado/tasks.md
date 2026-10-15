# Cash Flow and Daily Consolidation Tasks

## Execution Protocol (MANDATORY -- do not skip)

Implement these tasks with the `tlc-spec-driven` skill: **activate it by name and follow its Execute flow and Critical Rules.** Do not search for skill files by filesystem path. The skill is the source of truth for the full flow (per-task cycle, sub-agent delegation, adequacy review, Verifier, discrimination sensor).

---

**Design**: `.specs/features/fluxo-caixa-consolidado/design.md`  
**Status**: Ready for Approval  

---

## Test Coverage Matrix

> Generated from codebase, project guidelines, and spec - confirm before Execute. Guidelines found: none - strong defaults applied.

| Code Layer | Required Test Type | Coverage Expectation | Location Pattern | Run Command |
| ---------- | ------------------ | -------------------- | ---------------- | ----------- |
| Domain / Entities | unit | 100% branches; 1:1 with spec ACs; edge cases for negative amounts and invalid types | `tests/**/*.UnitTests/**/*.cs` | `dotnet test --filter "Category=Unit"` |
| Application / Handlers & Validators | unit | All validation paths, error handling, and command execution | `tests/**/*.UnitTests/**/*.cs` | `dotnet test --filter "Category=Unit"` |
| Messaging / Consumers & Workers | unit | Consumption behavior, idempotency, and outbox publication with TestHarness | `tests/**/*.UnitTests/**/*.cs` | `dotnet test --filter "Category=Unit"` |
| Controllers / APIs | integration | HTTP contracts, status codes (201, 200, 400), ProblemDetails | `tests/**/*.IntegrationTests/**/*.cs` | `dotnet test --filter "Category=Integration"` |
| Resilience & End-to-End | integration | Transactions API operation with Consolidation service offline and subsequent recovery | `tests/**/*.IntegrationTests/**/*.cs` | `dotnet test --filter "Category=Integration"` |

## Gate Check Commands

> Deterministic validation commands targeting .NET 9 LTS.

| Gate Level | When to Use | Command |
| ---------- | ----------- | ------- |
| Quick | After tasks with unit tests only | `dotnet test --filter "Category=Unit"` |
| Full | After tasks with integration tests | `dotnet test` |
| Build | After phase completion or infrastructure tasks | `dotnet build` |

---

## Execution Plan

Phases are ordered and run sequentially - each phase completes before the next begins, and tasks within a phase execute in order.

### Phase 1: Solution Setup & Shared Contracts

Scaffolding the .NET 9 Clean Architecture solution and shared event contracts.

```
T1 -> T2
```

### Phase 2: Transactions Domain & Application

Business rules for credits and debits, FluentValidation, and command handling with unit tests.

```
T3 -> T4 -> T5
```

### Phase 3: Transactions Infrastructure, Outbox & API

Relational persistence, Transactional Outbox pattern, messaging background worker, and REST API endpoints.

```
T6 -> T7 -> T8 -> T9
```

### Phase 4: Consolidation Domain, Consumer & API

Daily balance domain entity, idempotent MassTransit event consumer, persistence, and query controller with tests.

```
T10 -> T11 -> T12 -> T13
```

### Phase 5: Docker, Resilience Verification & Documentation

Docker container packaging, end-to-end resilience integration test, and comprehensive technical documentation.

```
T14 -> T15 -> T16
```

---

## Task Breakdown

### Phase 1: Solution Setup & Shared Contracts

#### T1: Create Solution and Clean Architecture Projects

**What**: Create the .NET solution `CashFlow.sln` and Clean Architecture project structure (.NET 9) with initial test setup.  
**Where**: `CashFlow.sln`  
**Depends on**: none  
**Reuses**: Standard dotnet sln and xunit templates  
**Requirement**: LANC-01  

**Tools**:
- MCP: `run_command`
- Skill: `tlc-spec-driven`

**Done when**:
- [ ] Solution created with Domain, Application, Infrastructure, Api, and Test projects
- [ ] All project references configured
- [ ] Gate check passes: `dotnet build`

**Tests**: unit  
**Gate**: quick  

---

#### T2: Define Shared Contracts and Event Definitions

**What**: Define the `TransactionRegisteredEvent` contract and shared enum `TransactionType` in the Shared project.  
**Where**: `src/Shared/CashFlow.Shared.Contracts/Events/TransactionRegisteredEvent.cs`  
**Depends on**: T1  
**Reuses**: C# record types and DateOnly  
**Requirement**: RESIL-01  

**Tools**:
- MCP: `write_to_file`
- Skill: `tlc-spec-driven`

**Done when**:
- [ ] Immutable `TransactionRegisteredEvent` record created
- [ ] `TransactionType` enum (Credit = 1, Debit = 2) defined
- [ ] Serialization and deserialization unit tests passing

**Tests**: unit  
**Gate**: quick  

---

### Phase 2: Transactions Domain & Application

#### T3: Implement Transaction Domain Entity

**What**: Implement the `Transaction` entity with factory methods, amount validation (> 0), and immutable state.  
**Where**: `src/Transactions/CashFlow.Transactions.Domain/Entities/Transaction.cs`  
**Depends on**: T2  
**Reuses**: Domain Driven Design aggregate root pattern  
**Requirement**: LANC-01  

**Tools**:
- MCP: `write_to_file`
- Skill: `tlc-spec-driven`

**Done when**:
- [ ] `CreateCredit` and `CreateDebit` factory methods implemented
- [ ] Rejection of amounts less than or equal to zero
- [ ] Unit tests covering all valid paths and domain exceptions

**Tests**: unit  
**Gate**: quick  

---

#### T4: Implement RegisterTransactionCommandValidator with FluentValidation

**What**: Implement validation rules for `RegisterTransactionCommand` with descriptive error messages.  
**Where**: `src/Transactions/CashFlow.Transactions.Application/Validators/RegisterTransactionCommandValidator.cs`  
**Depends on**: T3  
**Reuses**: FluentValidation package  
**Requirement**: LANC-03  

**Tools**:
- MCP: `write_to_file`
- Skill: `tlc-spec-driven`

**Done when**:
- [ ] Required field checks for date, description, and positive amount
- [ ] Standardized validation messages
- [ ] Unit tests covering valid and invalid payload scenarios

**Tests**: unit  
**Gate**: quick  

---

#### T5: Implement RegisterTransactionCommandHandler

**What**: Implement the command handler to register transactions and store an outbox event in the same transaction.  
**Where**: `src/Transactions/CashFlow.Transactions.Application/Commands/RegisterTransactionCommandHandler.cs`  
**Depends on**: T4  
**Reuses**: Repository interfaces and command DTOs  
**Requirement**: LANC-01  

**Tools**:
- MCP: `write_to_file`
- Skill: `tlc-spec-driven`

**Done when**:
- [ ] Handler orchestrates entity creation and transactional unit of work
- [ ] Outbox message instantiated with serialized event payload
- [ ] Unit tests with mocked repositories passing

**Tests**: unit  
**Gate**: quick  

---

### Phase 3: Transactions Infrastructure, Outbox & API

#### T6: Configure TransactionsDbContext and Persistence Entities

**What**: Configure `TransactionsDbContext` with Entity Framework Core mappings for `Transaction` and `OutboxMessage`.  
**Where**: `src/Transactions/CashFlow.Transactions.Infrastructure/Data/TransactionsDbContext.cs`  
**Depends on**: T5  
**Reuses**: Entity Framework Core  
**Requirement**: LANC-04  

**Tools**:
- MCP: `write_to_file`
- Skill: `tlc-spec-driven`

**Done when**:
- [ ] Relational mappings with types, precision, and constraints
- [ ] ACID transactional integrity between Transaction and OutboxMessage
- [ ] In-memory / SQLite persistence unit tests passing

**Tests**: unit  
**Gate**: quick  

---

#### T7: Implement TransactionRepository and OutboxRepository

**What**: Implement concrete repositories for transactions and pending outbox messages.  
**Where**: `src/Transactions/CashFlow.Transactions.Infrastructure/Repositories/TransactionRepository.cs`  
**Depends on**: T6  
**Reuses**: `TransactionsDbContext`  
**Requirement**: LANC-04  

**Tools**:
- MCP: `write_to_file`
- Skill: `tlc-spec-driven`

**Done when**:
- [ ] Transaction creation and retrieval methods implemented
- [ ] Outbox batch fetch (`GetPendingMessagesAsync`) and status update methods
- [ ] Repository unit tests passing

**Tests**: unit  
**Gate**: quick  

---

#### T8: Implement OutboxPublisherWorker Background Service

**What**: Create a background `IHostedService` polling pending outbox messages and publishing them to MassTransit.  
**Where**: `src/Transactions/CashFlow.Transactions.Infrastructure/BackgroundServices/OutboxPublisherWorker.cs`  
**Depends on**: T7  
**Reuses**: MassTransit `IPublishEndpoint` and `IOutboxRepository`  
**Requirement**: RESIL-01  

**Tools**:
- MCP: `write_to_file`
- Skill: `tlc-spec-driven`

**Done when**:
- [ ] Batch polling and resilient message dispatching
- [ ] Transient failure handling without crashing the service
- [ ] MassTransit TestHarness unit tests passing

**Tests**: unit  
**Gate**: quick  

---

#### T9: Implement TransactionsController and API Integration Tests

**What**: Expose REST endpoint `POST /api/v1/transactions` with Swagger documentation and integration tests.  
**Where**: `src/Transactions/CashFlow.Transactions.Api/Controllers/TransactionsController.cs`  
**Depends on**: T8  
**Reuses**: ASP.NET Core Web API and ProblemDetails  
**Requirement**: LANC-01  

**Tools**:
- MCP: `write_to_file`
- Skill: `tlc-spec-driven`

**Done when**:
- [ ] Endpoint returns HTTP 201 Created with transaction details
- [ ] Returns HTTP 400 Bad Request with RFC 7807 ProblemDetails for invalid input
- [ ] `WebApplicationFactory` integration tests passing

**Tests**: integration  
**Gate**: full  

---

### Phase 4: Consolidation Domain, Consumer & API

#### T10: Implement DailyBalance Domain Entity and Calculation Rules

**What**: Implement `DailyBalance` domain entity with credit/debit accumulation and net balance arithmetic.  
**Where**: `src/Consolidation/CashFlow.Consolidation.Domain/Entities/DailyBalance.cs`  
**Depends on**: T2  
**Reuses**: Domain Model Pattern  
**Requirement**: CONS-01  

**Tools**:
- MCP: `write_to_file`
- Skill: `tlc-spec-driven`

**Done when**:
- [ ] Accurate net balance calculation (`TotalCredits - TotalDebits`)
- [ ] Counter increments and timestamp tracking
- [ ] Unit tests covering multiple credits, debits, and zero balance scenarios

**Tests**: unit  
**Gate**: quick  

---

#### T11: Implement Idempotent TransactionRegisteredConsumer

**What**: Create MassTransit consumer to process `TransactionRegisteredEvent` and update daily balance idempotently.  
**Where**: `src/Consolidation/CashFlow.Consolidation.Application/Consumers/TransactionRegisteredConsumer.cs`  
**Depends on**: T10  
**Reuses**: MassTransit `IConsumer`  
**Requirement**: RESIL-02  

**Tools**:
- MCP: `write_to_file`
- Skill: `tlc-spec-driven`

**Done when**:
- [ ] Event consumption and balance update coordination
- [ ] Idempotency guard preventing duplicate calculations
- [ ] MassTransit InMemory TestHarness unit tests passing

**Tests**: unit  
**Gate**: quick  

---

#### T12: Implement DailyBalanceRepository and ConsolidationDbContext

**What**: Configure `ConsolidationDbContext` and implement `DailyBalanceRepository` supporting concurrent updates.  
**Where**: `src/Consolidation/CashFlow.Consolidation.Infrastructure/Repositories/DailyBalanceRepository.cs`  
**Depends on**: T11  
**Reuses**: Entity Framework Core  
**Requirement**: CONS-01  

**Tools**:
- MCP: `write_to_file`
- Skill: `tlc-spec-driven`

**Done when**:
- [ ] DateOnly primary key mapping and sub-10ms indexed queries
- [ ] Concurrency-safe upsert implementation
- [ ] Persistence and retrieval unit tests passing

**Tests**: unit  
**Gate**: quick  

---

#### T13: Implement DailyBalancesController and Query Endpoint

**What**: Expose REST endpoint `GET /api/v1/daily-balances?date=YYYY-MM-DD` returning daily balance summaries.  
**Where**: `src/Consolidation/CashFlow.Consolidation.Api/Controllers/DailyBalancesController.cs`  
**Depends on**: T12  
**Reuses**: ASP.NET Core Controller and memory caching  
**Requirement**: CONS-02  

**Tools**:
- MCP: `write_to_file`
- Skill: `tlc-spec-driven`

**Done when**:
- [ ] Returns HTTP 200 with daily totals
- [ ] Returns zeroed totals for dates with no registered transactions
- [ ] `WebApplicationFactory` integration tests passing

**Tests**: integration  
**Gate**: full  

---

### Phase 5: Docker, Resilience Verification & Documentation

#### T14: Create Dockerfiles and Docker Compose Setup

**What**: Create multi-stage Dockerfiles for both APIs and `docker-compose.yml` with RabbitMQ and healthchecks.  
**Where**: `docker-compose.yml`  
**Depends on**: T9, T13  
**Reuses**: Official Microsoft .NET and RabbitMQ container images  
**Requirement**: DOCK-01  

**Tools**:
- MCP: `write_to_file`
- Skill: `tlc-spec-driven`

**Done when**:
- [ ] Multi-stage production-ready Dockerfiles for both services
- [ ] `docker-compose.yml` with automated container healthchecks
- [ ] Compose syntax validation and build testing

**Tests**: integration  
**Gate**: full  

---

#### T15: Implement End-to-End Resilience Integration Test

**What**: Implement an automated test proving the Transactions service remains fully functional with Consolidation offline.  
**Where**: `tests/CashFlow.IntegrationTests/Resilience/ResilienceScenarioTests.cs`  
**Depends on**: T14  
**Reuses**: `WebApplicationFactory` and MassTransit TestHarness  
**Requirement**: RESIL-01  

**Tools**:
- MCP: `write_to_file`
- Skill: `tlc-spec-driven`

**Done when**:
- [ ] Offline consolidation simulation while transactions receive HTTP 201 Created
- [ ] Service recovery simulation and exact final balance verification without message loss
- [ ] Resilience test passing with 100% green gate

**Tests**: integration  
**Gate**: full  

---

#### T16: Create Comprehensive Technical README Documentation

**What**: Write a detailed `README.md` covering architecture, C4/flow diagrams, local execution, and test commands.  
**Where**: `README.md`  
**Depends on**: T15  
**Reuses**: Approved spec and design artifacts  
**Requirement**: DOC-01  

**Tools**:
- MCP: `write_to_file`
- Skill: `tlc-spec-driven`

**Done when**:
- [ ] Step-by-step instructions for running locally via dotnet CLI and via Docker Compose
- [ ] Sample cURL commands for all REST endpoints
- [ ] Architecture diagrams and design pattern explanations (Outbox, Clean Architecture, SOLID)

**Tests**: unit  
**Gate**: build  
