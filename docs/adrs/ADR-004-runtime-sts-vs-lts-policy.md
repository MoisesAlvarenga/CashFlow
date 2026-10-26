# ADR-004: Decisão de Runtime .NET 9 STS e Alinhamento com .NET 10 LTS

## Contexto

A política de lançamentos da Microsoft estabelece uma cadência anual dividida em dois tipos de suporte:
* **LTS (Long Term Support)**: Versões pares (.NET 6, .NET 8, .NET 10), com 36 meses de suporte oficial;
* **STS (Standard Term Support)**: Versões ímpares (.NET 7, .NET 9), com 18 meses de suporte oficial.

Sistemas corporativos exigem conformidade com requisitos de governança de TI que requerem o uso em produção de versões com janelas estendidas de suporte (**LTS**). Com a disponibilidade do **.NET 10 LTS** (suporte oficial até Novembro de 2028), estabeleceu-se a diretriz de desenvolvimento para adoção do .NET 10 LTS em todas as frentes evolutivas.

## Decisão Arquitetural

1. **Implementação Inicial de Referência em .NET 9 STS com C# 13**:
   * O projeto adotou inicialmente **.NET 9 STS**, usufruindo de:
     * Otimizações de compilação JIT com *Dynamic PGO*;
     * Adaptação dinâmica de limites de heap no *Server Garbage Collector*;
     * Recursos de linguagem modernos: *params collections* (`ReadOnlySpan<T>`), o novo primitivo leve `System.Threading.Lock` e melhorias de escape analysis em estruturas de memória.
2. **Diretriz de Alinhamento e Migração para .NET 10 LTS (EVO-01)**:
   * Conforme diretriz de governança corporativa, o desenvolvimento evolutivo adota **.NET 10 LTS** como versão definitiva e unificada de longo prazo;
   * A esteira de engenharia e os SDKs do ambiente (10.0.x) já estão preparados para a transição transparente para `<TargetFramework>net10.0</TargetFramework>` e orquestração via .NET Aspire.

## Consequências

### Positivas
* Máxima performance, menor latência de GC e uso de recursos modernos de ponta da plataforma;
* Total estabilidade operacional com suporte oficial de 3 anos do .NET 10 LTS;
* Eliminação de débitos técnicos e alinhamento perfeito com o roadmap moderno do .NET.
