# ADR-004: Decisão de Runtime .NET 9 STS e Política de Compatibilidade com .NET 8 LTS

## Contexto

A política de lançamentos da Microsoft estabelece uma cadência anual dividida em dois tipos de suporte:
* **LTS (Long Term Support)**: Versões pares (.NET 6, .NET 8, .NET 10), com 36 meses de suporte oficial;
* **STS (Standard Term Support)**: Versões ímpares (.NET 7, .NET 9), com 18 meses de suporte oficial.

Sistemas corporativos frequentemente exigem conformidade com requisitos de governança de TI que limitam o uso em produção a versões com janelas estendidas de suporte (LTS), enquanto times de engenharia buscam maximizar throughput e adotar melhorias modernas de linguagem e compilador.

## Decisão Arquitetural

1. **Adoção de .NET 9 STS para Implementação de Referência**:
   * O projeto adota **.NET 9 STS com C# 13**, beneficiando-se das evoluções mais recentes:
     * Otimizações de compilação JIT com *Dynamic PGO*;
     * Adaptação dinâmica de limites de heap no *Server Garbage Collector*;
     * Novos recursos de linguagem: *params collections* (`ReadOnlySpan<T>`), o novo primitivo leve `System.Threading.Lock` e melhorias de escape analysis em estruturas de memória.
2. **Garantia de 100% de Compatibilidade Reversa com .NET 8 LTS**:
   * O código-fonte respeita os padrões universais do ecossistema .NET corporativo, sem dependências proprietárias ou acoplamentos exclusivos da versão STS.
   * Caso uma esteira de implantação corporativa exija estritamente uma versão LTS, a solução pode ser recompilada para **.NET 8 LTS** apenas alterando o `<TargetFramework>` nos projetos `.csproj` de `net9.0` para `net8.0`.

## Consequências

### Positivas
* Máxima performance, menor latência de GC e uso de recursos modernos de ponta da plataforma;
* Caminho facilitado e sem quebras para migração ao futuro **.NET 10 LTS**;
* Flexibilidade total para atender ambientes com políticas corporativas restritivas via .NET 8 LTS.
