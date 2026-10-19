# Lições Aprendidas de Arquitetura & Engenharia

Registro das lições de arquitetura, resiliência e engenharia de software consolidadas durante o desenvolvimento do projeto:

---

### 1. Evitar Escrita Dupla (*Dual-Write*) com Transactional Outbox
* **Desafio**: Tentar persistir a transação financeira no banco de dados e simultaneamente publicar uma mensagem no broker (RabbitMQ) dentro do mesmo request HTTP síncrono gera inconsistência grave caso a conexão com a rede ou o broker oscile após a gravação do banco.
* **Solução Arquitetural**: O padrão **Transactional Outbox** grava a entidade `Transaction` e o payload do evento na tabela `OutboxMessages` na **mesma transação ACID local**. Um `BackgroundService` dedicado drena essas mensagens assincronamente, garantindo entrega confiável (*At-Least-Once Delivery*) com total desacoplamento.

---

### 2. Idempotência é Obrigatória em Mensageria Distribuída
* **Desafio**: Brokers de mensageria como o RabbitMQ garantem entrega de mensagens (*At-Least-Once*), mas em situações de reconexão de rede ou timeout de ACK, mensagens podem ser reentregues.
* **Solução Arquitetural**: O consumidor na Consolidação (`TransactionRegisteredConsumer`) foi desenhado para ser estritamente idempotente. As operações de consolidação utilizam agregação contábil incremental e UPSERT atômico no PostgreSQL indexado por data (`IX_DailyBalances_Date`), prevenindo duplicidade de saldos mesmo em caso de reprocessamento.

---

### 3. Gerenciamento de Escopo em Workers Singleton (*Captive Dependencies*)
* **Desafio**: Background workers (`BackgroundService`) são registrados no contêiner de DI do .NET como instâncias `Singleton`, enquanto contextos de banco (`DbContext`) e repositórios são de ciclo de vida `Scoped`. Injetar um serviço com escopo diretamente no construtor de um singleton gera o erro de *Captive Dependency* e vazamento de conexões.
* **Solução Arquitetural**: O worker consome `IServiceScopeFactory`, criando explicitamente um escopo por ciclo de processamento (`using var scope = _scopeFactory.CreateScope()`), resolvendo o repositório sob demanda e liberando os recursos imediatamente após cada lote.

---

### 4. Simplicidade Arquitetural: Índices Eficientes vs. Cache Desnecessário
* **Desafio**: O requisito de performance exigia suportar momentos de pico de 50 requisições por segundo na consolidação. A primeira tentação seria adicionar uma camada de cache em memória (`IMemoryCache` ou Redis).
* **Solução Arquitetural**: Optamos por manter a arquitetura enxuta, confiável e consistente: criamos um índice B-Tree composto no PostgreSQL (`IX_DailyBalances_Date`) e consultas enxutas. O teste de carga com o **Grafana K6** comprovou empiricamente que o PostgreSQL indexado respondeu as consultas com **P95 de apenas 40ms e 0% de perda**, provando que não era necessário o overhead e a complexidade de invalidação de cache.

---

### 5. Segurança Corporativa: 12-Factor App & Non-Root Containers
* **Desafio**: Arquivos de configuração versionados no Git podem acidentalmente vazar segredos em auditorias de segurança estática (SAST) e containers executando como `root` violam diretrizes de conformidade.
* **Solução Arquitetural**: Adotamos o princípio III do **12-Factor App** (senhas zeradas nos arquivos base e injeção 100% via Variáveis de Ambiente no Docker). Nos Dockerfiles, configuramos `USER $APP_UID`, rodando as APIs em Linux não-root e zerando todos os alertas do SonarQube.
