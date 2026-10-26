# Fluxo de Caixa e Consolidação Diária Specification

## Problem Statement

Lojistas precisam gerenciar com precisão seu fluxo financeiro diário, registrando entradas (créditos) e saídas (débitos) e obtendo relatórios confiáveis do saldo consolidado de cada dia. A arquitetura exige alta disponibilidade, de modo que a gestão de lançamentos permaneça 100% operante mesmo diante de falhas na consolidação, além de suportar picos de 50 requisições por segundo na consolidação com perda máxima tolerada de 5%.

## Goals

- [ ] Disponibilizar API para registro e consulta de lançamentos financeiros (crédito e débito) com validações robustas.
- [ ] Fornecer relatório e endpoint de consulta para saldo diário consolidado (total créditos, total débitos e saldo líquido).
- [ ] Assegurar desacoplamento e resiliência com mensageria assíncrona, garantindo que indisponibilidade na consolidação não afete o registro de lançamentos.
- [ ] Atender aos requisitos de vazão e resiliência (50 req/s com perda máxima de 5% em horários de pico).
- [ ] Entregar suite automatizada de testes (unitários e integração) cobrindo regras de negócio, limites e falhas.
- [ ] Estruturar a solução em C# com Clean Architecture, SOLID e Design Patterns, empacotada com Docker e documentada com clareza.

## Out of Scope

| Feature | Reason |
| ------- | ------ |
| Autenticação corporativa complexa (OAuth2/OpenID Connect multi-tenant) | Foco central nas regras de negócio, resiliência e concorrência do desafio técnico |
| Múltiplas moedas e conversão cambial | O sistema opera exclusivamente em moeda corrente nacional (BRL) |
| Interface visual / Frontend Web ou Mobile | Entrega focada no backend (APIs REST documentadas via OpenAPI/Swagger) |
| Emissão fiscal ou integração bancária direta (PIX/TED/Boletos) | Escopo estritamente delimitado ao livro-caixa e consolidação de saldos |

---

## Assumptions & Open Questions

| Assumption / decision | Chosen default | Rationale | Confirmed? |
| --------------------- | -------------- | --------- | ---------- |
| Versão da plataforma C# / .NET | .NET 10 (LTS) / C# 13 | Plataforma de suporte de longo prazo (36 meses) para ambientes corporativos com orquestração .NET Aspire | Sim |
| Estratégia de desacoplamento entre serviços | Mensageria assíncrona orientada a eventos (ex: RabbitMQ / MassTransit) com Outbox Pattern | Garante que lançamentos são salvos com sucesso mesmo se o consumidor de consolidação estiver inoperante | Sim |
| Modelo de persistência dos lançamentos | Banco relacional com transação atômica e idempotência por identificador único | Previne duplicidade de lançamentos e garante consistência dos créditos e débitos | Sim |
| Mecanismo de consolidação diária | Processamento de eventos por data de competência com agregação de saldos e cache/materialized view | Permite consultas de saldo em milissegundos mesmo sob carga de 50 chamadas/s | Sim |
| Tolerância a perdas (máx 5%) em picos de 50 req/s | Filas com dead-letter, circuit breaker e retenção persistente de mensagens | O sistema foi desenhado para perda zero na prática, utilizando a margem de 5% apenas como limite de SLA sob sobrecarga extrema | Sim |

**Open questions:** none - all resolved or logged above (required before the spec is confirmed).

---

## User Stories

### P1: Registro de Lançamentos Financeiros (Créditos e Débitos) ⭐ MVP

**User Story**: As a lojista, I want registrar lançamentos de crédito e débito no sistema so that meu fluxo de caixa reflita todas as entradas e saídas financeiras da minha loja.

**Why P1**: É a capacidade primária de ingestão financeira do negócio. Sem lançamentos, não existe fluxo de caixa nem consolidação.

**Acceptance Criteria**:

1. WHEN um lojista envia uma requisição válida de crédito THEN the system SHALL registrar o lançamento com identificador único, data, descrição e valor positivo.
2. WHEN um lojista envia uma requisição válida de débito THEN the system SHALL registrar o lançamento com identificador único, data, descrição e valor positivo categorizado como débito.
3. IF o valor do lançamento for menor ou igual a zero THEN the system SHALL rejeitar a requisição com código HTTP 400 e mensagem explicativa de validação.
4. IF a data ou descrição do lançamento for inválida ou omitida THEN the system SHALL rejeitar a requisição com código HTTP 400 e detalhes do erro.
5. The system SHALL atribuir um identificador único imutável e timestamp de criação para cada lançamento persistido.

**Independent Test**: Enviar requisições POST para `/api/v1/transactions` com dados de crédito e débito, validando persistência, resposta HTTP 201 e rejeição adequada com HTTP 400 para valores negativos ou nulos.

---

### P1: Consulta do Saldo Diário Consolidado ⭐ MVP

**User Story**: As a lojista, I want consultar o saldo consolidado de uma data específica so that eu saiba o total de créditos, débitos e o saldo líquido resultante daquele dia.

**Why P1**: É o produto final de inteligência de negócio exigido pelo lojista para tomada de decisão financeira diária.

**Acceptance Criteria**:

1. WHEN um usuário solicita o saldo consolidado para uma data válida THEN the system SHALL retornar o total de créditos, o total de débitos e o saldo líquido calculado daquele dia.
2. IF não existirem lançamentos registrados para a data solicitada THEN the system SHALL retornar totais zerados e saldo líquido igual a zero com status HTTP 200.
3. IF o formato da data na consulta for inválido THEN the system SHALL retornar HTTP 400 com indicação clara do formato esperado (YYYY-MM-DD).
4. The system SHALL calcular o saldo líquido diário como a soma total dos créditos subtraída da soma total dos débitos do respectivo dia.

**Independent Test**: Registrar múltiplos créditos e débitos para um dia determinado e enviar GET para `/api/v1/daily-balances?date=YYYY-MM-DD`, checando se o total de créditos, débitos e saldo batem com precisão aritmética.

---

### P1: Desacoplamento e Resiliência Operacional

**User Story**: As a operador do sistema, I want que o registro de lançamentos continue operando mesmo com o serviço de consolidação fora do ar so that a operação de caixa da loja nunca seja interrompida por falhas externas.

**Why P1**: Requisito não-funcional mandatório do desafio técnico que impede impacto em cascata no negócio.

**Acceptance Criteria**:

1. WHILE o serviço de consolidação diária estiver indisponível or com falhas the system SHALL continuar aceitando, persistindo e confirmando novos lançamentos financeiros com sucesso (HTTP 201).
2. WHEN o serviço de consolidação for restabelecido THEN the system SHALL processar de forma idempotente todos os lançamentos acumulados durante o período de indisponibilidade.
3. The system SHALL isolar a infraestrutura de gravação de lançamentos da infraestrutura de agregação do saldo consolidado.

**Independent Test**: Desligar intencionalmente o consumidor de consolidação, emitir 20 lançamentos na API de lançamentos (confirmando sucesso em todos), religar a consolidação e verificar se todos os 20 lançamentos foram consolidados no saldo do dia.

---

### P2: Suporte a Picos de Carga e Vazão (50 req/s)

**User Story**: As a arquiteto de software, I want que o sistema de consolidação suporte momentos de pico de 50 chamadas por segundo so that o sistema absorva alto volume de concorrência com perda inferior a 5%.

**Why P2**: Requisito não-funcional de desempenho e robustez para cenários de alta demanda.

**Acceptance Criteria**:

1. WHILE o sistema de consolidação recebe picos de até 50 requisições simultâneas por segundo the system SHALL processar as demandas com taxa de sucesso superior ou igual a 95%.
2. IF ocorrer sobrecarga momentânea de chamadas THEN the system SHALL amortecer o processamento utilizando filas de mensageria assíncronas e buffers em memória sem degradação do serviço de lançamentos.
3. The system SHALL responder às consultas de saldo diário consolidado com tempo de resposta p95 inferior a 200 milissegundos.

**Independent Test**: Executar teste de carga simulando 50 req/s contínuas e aferir taxa de erro (< 5%) e latência p95.

---

### P2: Empacotamento em Containers e Orquestração Local

**User Story**: As a desenvolvedor e avaliador, I want executar todo o ecossistema com Docker Compose so that a aplicação, os bancos de dados e mensageria subam localmente de forma reproduzível com um único comando.

**Why P2**: Requisito opcional que acelera o teste da aplicação, a avaliação técnica e elimina dependências manuais de ambiente.

**Acceptance Criteria**:

1. WHEN o comando `docker-compose up` for executado na raiz do projeto THEN the system SHALL inicializar todos os serviços, dependências de banco de dados e mensageria em containers saudáveis.
2. WHERE a execução ocorrer via Docker Compose the system SHALL disponibilizar os endpoints HTTP nas portas configuradas e prontos para teste imediato.
3. The system SHALL incluir healthchecks nos containers garantindo inicialização ordenada das dependências.

**Independent Test**: Executar `docker compose up --build` e verificar que todas as APIs respondem aos probes de saúde (health checks) e requisições HTTP normais.

---

## Edge Cases

- IF um lançamento duplicado for submetido com a mesma chave de idempotência THEN the system SHALL retornar o registro original sem aplicar débito ou crédito duplicado.
- IF ocorrer falha de conexão de rede durante a publicação da mensagem de evento THEN the system SHALL reter o evento no Outbox relacional para reenvio seguro na próxima tentativa.
- WHEN múltiplos lançamentos concorrentes para o mesmo dia forem processados em paralelo the system SHALL garantir a consistência matemática do saldo diário consolidado sem condições de corrida.

---

## Requirement Traceability

| Requirement ID | Story | Phase | Status |
| -------------- | ----- | ----- | ------ |
| LANC-01 | P1: Registro de Lançamentos Financeiros | Design | Pending |
| LANC-02 | P1: Registro de Lançamentos Financeiros | Design | Pending |
| LANC-03 | P1: Registro de Lançamentos Financeiros | Design | Pending |
| LANC-04 | P1: Registro de Lançamentos Financeiros | Design | Pending |
| CONS-01 | P1: Consulta do Saldo Diário Consolidado | Design | Pending |
| CONS-02 | P1: Consulta do Saldo Diário Consolidado | Design | Pending |
| RESIL-01 | P1: Desacoplamento e Resiliência Operacional | Design | Pending |
| RESIL-02 | P1: Desacoplamento e Resiliência Operacional | Design | Pending |
| PERF-01 | P2: Suporte a Picos de Carga e Vazão | Design | Pending |
| PERF-02 | P2: Suporte a Picos de Carga e Vazão | Design | Pending |
| DOCK-01 | P2: Empacotamento em Containers e Orquestração Local | Design | Pending |
| DOCK-02 | P2: Empacotamento em Containers e Orquestração Local | Design | Pending |

---

## Success Criteria

- [ ] 100% dos requisitos mandatórios do desafio implementados (C#, Clean Code, SOLID, testes automatizados, documentação e desacoplamento).
- [ ] API de lançamentos operando com 100% de disponibilidade mesmo com o serviço de consolidação deliberadamente desligado em teste de integração.
- [ ] Bateria de testes unitários e de integração passando com sucesso em pipeline automatizado.
- [ ] Teste de carga demonstrando suporte a 50 requisições por segundo na consolidação com taxa de perda inferior a 5%.
- [ ] Subida completa e automática do ambiente via Docker Compose e documentação detalhada no README.md.
