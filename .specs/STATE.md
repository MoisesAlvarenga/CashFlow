# Architecture Decisions & Project State

## Architectural Decisions (ADRs)

### AD-001: Desacoplamento Assíncrono com Microsserviços Autônomos
- **Decisão**: Arquitetura desacoplada em dois serviços principais (`CashFlow.Transactions.Api` e `CashFlow.Consolidation.Api`) comunicando-se de forma assíncrona orientada a eventos.
- **Autor / Responsável**: Decisão tomada pelo Arquiteto / Desenvolvedor.
- **Motivo**: Atender rigorosamente ao requisito não-funcional mandatório que exige que a aplicação de lançamentos continue operante mesmo na indisponibilidade do sistema de consolidação.
- **Trade-off**: Complexidade de consistência eventual e necessidade de broker de mensageria em relação a chamadas síncronas HTTP.
- **Escopo**: Arquitetura global, topologia de microsserviços e resiliência.
- **Data**: 2026-10-15
- **Status**: Ativo / Implementado

### AD-002: .NET 9 LTS com Clean Architecture e SOLID
- **Decisão**: Utilização de .NET 9 LTS / C# 13 com Clean Architecture (Domain, Application, Infrastructure, Api) e aplicação estrita dos 5 princípios SOLID.
- **Autor / Responsável**: Decisão tomada pelo Arquiteto / Desenvolvedor.
- **Motivo**: Requisito técnico mandatório (C#, Clean Code, SOLID, isolamento de camadas entre infra, aplicação e domínio).
- **Trade-off**: Criação de múltiplos projetos e interfaces no início da solução, compensada por testabilidade determinística (51 testes em ~260ms) e manutenibilidade.
- **Escopo**: Estrutura dos projetos C# e regras de negócio.
- **Data**: 2026-10-15
- **Status**: Ativo / Implementado

### AD-003: Transactional Outbox Pattern e RabbitMQ Nativo (RabbitMQ.Client 7)
- **Decisão**: Adoção do padrão Transactional Outbox com BackgroundService e utilização do driver oficial `RabbitMQ.Client v7.2.2` (com Direct Exchange `cashflow.transactions` e manual `BasicAckAsync`/`BasicNackAsync`), dispensando frameworks pesados de terceiros (como MassTransit).
- **Autor / Responsável**: Decisão tomada pelo Arquiteto / Desenvolvedor.
- **Motivo**: Eliminar o problema de escrita dupla (*Dual-Write*) com garantia ACID no PostgreSQL, mantendo o consumo de memória ultrabaixo e controle granular sobre pré-busca (`BasicQos`), retentativas e Dead Letter Queue (DLQ).
- **Trade-off**: Implementação explícita do worker de despacho e consumidor idempotente, compensada por maior controle e zero overhead desnecessário.
- **Escopo**: Camada de persistência, mensageria e resiliência de rede.
- **Data**: 2026-10-15
- **Status**: Ativo / Implementado

### AD-004: Nomenclatura do Código-Fonte em Inglês
- **Decisão**: Padrão de nomenclatura de código-fonte (classes, interfaces, métodos, eventos, entidades e projetos) estritamente em inglês.
- **Autor / Responsável**: Decisão tomada pelo Arquiteto / Desenvolvedor.
- **Motivo**: Alinhamento com as melhores práticas globais de engenharia de software da indústria .NET e manutenibilidade por equipes multidisciplinares.
- **Trade-off**: Tradução dos termos de negócio (Lançamento -> Transaction, Saldo Diário -> DailyBalance, Consolidação -> Consolidation).
- **Escopo**: Todo o código-fonte, namespaces, classes, propriedades e arquivos C#.
- **Data**: 2026-10-15
- **Status**: Ativo / Implementado

### AD-005: Engenharia Assistida por IA com Governança Arquitetural Humana (Human-in-the-Loop)
- **Decisão**: Utilização de inteligência artificial generativa estritamente como ferramenta de assistência para aceleração de escrita de código boilerplate, validações repetitivas e scaffolding de testes, mantendo **100% das decisões de arquitetura, trade-offs, escolhas de design patterns e governança sob controle e validação exclusiva do desenvolvedor**.
- **Autor / Responsável**: Decisão tomada pelo Arquiteto / Desenvolvedor.
- **Motivo**: Aceleração do tempo de entrega (Time-to-Market) sem abrir mão de rigor de engenharia, integridade de código, qualidade estática e cobertura.
- **Trade-off**: Necessidade de revisão e auditoria contínua de todo o código gerado pelo desenvolvedor humano através de linters, SonarQube e suíte de testes automatizados.
- **Escopo**: Processo de desenvolvimento e governança do projeto.
- **Data**: 2026-10-15
- **Status**: Ativo / Implementado

### AD-006: Comprovação Empírica de Performance com Grafana K6 e Quality Gates
- **Decisão**: Implementação de testes de carga contínua com Grafana K6 e auditoria contínua via SonarQube Community Edition e Solidify.
- **Autor / Responsável**: Decisão tomada pelo Arquiteto / Desenvolvedor.
- **Motivo**: Provar empiricamente que o sistema suporta o requisito não-funcional de 50 chamadas/segundo com perda inferior a 5% (atingido 0% de erro e P95 < 45ms) e certificar conformidade Clean Code (0 bugs, 0 vulnerabilidades).
- **Escopo**: Esteira de qualidade, testes não-funcionais e auditoria.
- **Data**: 2026-10-18
- **Status**: Ativo / Implementado

---

## Status da Solução & Handoff

- **Feature**: fluxo-caixa-consolidado
- **Fase Atual**: Todas as Fases Concluídas com Sucesso (100% Entregue)
- **Status da Compilação**: 0 Erros, 0 Avisos (`dotnet build CashFlow.sln`)
- **Status dos Testes Unitários**: 51/51 Aprovados (Duração: ~260ms)
- **Status dos Testes de Carga (K6)**: 10 VUs / 50+ req/s com 0.00% de erro e P95 < 45ms
- **Status do SonarQube**: 0 Bugs, 0 Vulnerabilidades, 0 Security Hotspots, 0 Code Smells
- **Containers**: PostgreSQL 16, RabbitMQ 3.13, Transactions API, Consolidation API e SonarQube operacionais via Docker Compose
- **Branch**: master
