# ADR-004: Decisão de Runtime .NET 10 LTS e Orquestração .NET Aspire

## Status: Implementado (EVO-01)

## Contexto

A política de lançamentos da Microsoft estabelece uma cadência anual dividida em dois tipos de suporte:
* **LTS (Long Term Support)**: Versões pares (.NET 6, .NET 8, .NET 10), com 36 meses de suporte oficial;
* **STS (Standard Term Support)**: Versões ímpares (.NET 7, .NET 9), com 18 meses de suporte oficial.

Sistemas corporativos exigem conformidade com requisitos de governança de TI que requerem o uso em produção de versões com janelas estendidas de suporte (**LTS**). Com a disponibilidade do **.NET 10 LTS** (suporte oficial até Novembro de 2028), estabeleceu-se a diretriz corporativa de migrar toda a solução CashFlow para o .NET 10 LTS e adotar **.NET Aspire** como framework de orquestração de microsserviços e padrões de resiliência/observabilidade.

## Decisão Arquitetural

1. **Adoção Unificada de .NET 10 LTS**:
   * Todos os projetos da solução (`CashFlow.Shared.Contracts`, `CashFlow.Transactions.*`, `CashFlow.Consolidation.*`, testes e orquestração) utilizam `<TargetFramework>net10.0</TargetFramework>`;
   * Utilização de bibliotecas oficiais na versão `10.0.0` (`Microsoft.Extensions.*`, `Microsoft.AspNetCore.*`, `Npgsql.EntityFrameworkCore.PostgreSQL 10.0.0`);
   * Benefícios de compilação JIT de ponta, dynamic PGO avançado, otimizações de Garbage Collection e estabilidade corporativa garantida de 36 meses.

2. **Orquestração e Resiliência com .NET Aspire**:
   * Criação de `CashFlow.AppHost` (`Aspire.AppHost.Sdk 13.6.4` e pacotes `Aspire.Hosting.* 13.6.4`) centralizando a orquestração de containers locais (PostgreSQL, RabbitMQ com plugin de gerenciamento, Redis para cache distribuído) e dependências de inicialização (`WaitFor`);
   * Criação de `CashFlow.ServiceDefaults` padronizando Service Discovery, resiliência HTTP (`AddStandardResilienceHandler`), OpenTelemetry OTLP e health checks liveness/readiness (`/health`, `/alive`).

## Consequências

### Positivas
* Total conformidade com governança corporativa de ciclo de vida LTS (36 meses de suporte oficial);
* Experiência unificada de desenvolvimento e inicialização via Aspire Dashboard e AppHost;
* Máxima performance, menor latência de GC e total integridade de tracing e métricas distribuídas;
* Eliminação de débitos técnicos com alinhamento na versão de vanguarda do ecossistema .NET.
