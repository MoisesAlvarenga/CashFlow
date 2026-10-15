# STATE

## Decisions

### AD-001
- **Decision**: Arquitetura desacoplada em dois serviços principais (Serviço de Lançamentos e Serviço de Consolidação Diária) comunicando-se de forma assíncrona via mensageria/eventos.
- **Reason**: Atender rigorosamente ao requisito não-funcional mandatório que exige que a aplicação de lançamentos continue operante mesmo na indisponibilidade do sistema de consolidação.
- **Trade-off**: Complexidade de consistência eventual e necessidade de broker de mensageria em relação a chamadas síncronas HTTP ou banco único.
- **Scope**: Arquitetura global, comunicação entre serviços e resiliência.
- **Date**: 2026-10-15
- **Status**: active

### AD-002
- **Decision**: Utilização de .NET 9 LTS / C# com Clean Architecture (Domain, Application, Infrastructure, Presentation) e princípios SOLID.
- **Reason**: Requisito técnico mandatório (C#, Clean Code, SOLID, isolamento de camadas entre infra, aplicação e domínio).
- **Trade-off**: Criação de múltiplos projetos e abstrações no início da solução, compensada por alta testabilidade e manutenibilidade.
- **Scope**: Estrutura dos projetos C# e regras de negócio.
- **Date**: 2026-10-15

### AD-003
- **Decision**: Padrão Transactional Outbox e MassTransit para mensageria assíncrona entre Lançamentos e Consolidação.
- **Reason**: Garantir consistência transacional local (ACID) na gravação de lançamentos e desacoplamento com publicação assíncrona tolerante a falhas do broker ou da consolidação.
- **Trade-off**: Necessidade de tabela de outbox e worker de despacho de mensagens em segundo plano.
- **Scope**: Camada de persistência e comunicação entre microsserviços.
- **Date**: 2026-10-15
### AD-004
- **Decision**: Padrão de nomenclatura de código-fonte (classes, interfaces, métodos, eventos, entidades e projetos) estritamente em inglês.
- **Reason**: Alinhamento com boas práticas da indústria em C#/.NET e solicitação explícita para manter o código idiomático e internacionalizável.
- **Trade-off**: Tradução dos termos de negócio (Lançamento -> Transaction, Saldo Diário -> DailyBalance, Consolidação -> Consolidation).
- **Scope**: Todo o código-fonte, namespaces, classes, propriedades e arquivos C#.
- **Date**: 2026-10-15
- **Status**: active

## Handoff

- **Feature**: fluxo-caixa-consolidado
- **Phase / Task**: Phase 3 - Tasks
- **Completed**: none
- **In-progress** (file:line): none
- **Next step**: Aprovação do plano de tarefas e início da Fase 1 (T1: Criar Solution e Estrutura de Projetos .NET)
- **Blockers**: none
- **Uncommitted files**: none
- **Branch**: main
