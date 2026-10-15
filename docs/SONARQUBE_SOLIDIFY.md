# Integração SonarQube + Solidify

Este documento descreve a arquitetura, configuração e fluxo operacional da integração entre o **SonarQube** e o **Solidify** no projeto **Desafio Técnico Verity (Fluxo de Caixa e Consolidação Diária)**.

---

## 1. Visão Geral e Papéis

```
+---------------------------------------------------------------------------------+
|                                 SOLIDIFY ENGINE                                 |
|                                                                                 |
|   +-----------------------+   +-----------------------+   +------------------+  |
|   |      SOLID (50%)      |   |  Static Quality (15%) |   |  Security (15%)  |  |
|   |  Single Responsibility|   |       SonarQube       |   |  Gitleaks / SAST |  |
|   |  Open-Closed          |   |  Bugs, Smells, Dupl.  |   +------------------+  |
|   |  Liskov Substitution  |   +-----------------------+   +------------------+  |
|   |  Interface Segreg.    |   +-----------------------+   |    Tests (10%)   |  |
|   |  Dependency Inversion |   |   Performance (5%)    |   |  Unit + Integr.  |  |
|   +-----------------------+   |          k6           |   +------------------+  |
|                               +-----------------------+                         |
+---------------------------------------------------------------------------------+
```

* **Solidify (https://github.com/daraujo85/solidify)**:
  * Atua como o **Quality Gate de Release** e árbitro arquitetural.
  * Avalia o código contra os 5 princípios fundamentais do **SOLID** (responsável por **50% do Quality Score**).
  * Executa revisão por pares (Peer A e Peer B cegos) e arbitragem com IA para garantir que a arquitetura não sofra degradação.
* **SonarQube**:
  * Fornece evidências objetivas e determinísticas de **Qualidade Estática** (bugs, vulnerabilidades, code smells, duplicações e cobertura).
  * Alimenta diretamente o pilar de **Static Quality (15% do score)** do Solidify através da API REST do SonarQube (`/api/measures/component`, `/api/qualitygates/project_status`, `/api/issues/search`).

---

## 2. Composição do Quality Score

O Solidify unifica todas as dimensões de engenharia em uma pontuação objetiva de **0 a 100**:

$$\text{Quality Score} = (\text{SOLID} \times 0.50) + (\text{Static Quality / Sonar} \times 0.15) + (\text{Security} \times 0.15) + (\text{Tests} \times 0.10) + (\text{Performance} \times 0.05) + (\text{Frontend} \times 0.05)$$

### Normalização das Métricas do SonarQube para o Solidify
O Solidify calcula a nota do pilar estático a partir de 100 pontos base, aplicando penalidades proporcionais à severidade das ocorrências do Sonar:
* **Blocker / Critical**: -30 pontos cada
* **Major / High**: -15 pontos cada
* **Minor / Medium**: -5 pontos cada
* **Info / Low**: -1 ponto cada
* *Score Mínimo*: 0

O status nativo do Quality Gate do SonarQube (`PASSED`, `ERROR`, `WARN`) é preservado e apresentado de forma independente no relatório de auditoria do Solidify.

---

## 3. Arquivos de Configuração Implementados

| Arquivo | Finalidade |
|---|---|
| [`solidify.json`](file:///C:/DEV/desafio_tecnico_Verity/solidify.json) | Configuração principal do Solidify, com `analyzers.sonar` habilitado e parametrizado. |
| [`sonar-project.properties`](file:///C:/DEV/desafio_tecnico_Verity/sonar-project.properties) | Parâmetros de análise estática do SonarScanner (.NET C#, relatórios Cobertura/TRX, exclusões). |
| [`docker-compose.sonar.yml`](file:///C:/DEV/desafio_tecnico_Verity/docker-compose.sonar.yml) | Docker Compose para subir o servidor local SonarQube Community Edition na porta `9000`. |
| [`scripts/run-sonar.ps1`](file:///C:/DEV/desafio_tecnico_Verity/scripts/run-sonar.ps1) | Automação completa: begin scanner → build → test com cobertura → end scanner. |
| [`scripts/solidify-audit.ps1`](file:///C:/DEV/desafio_tecnico_Verity/scripts/solidify-audit.ps1) | Automação da execução do Solidify (`doctor`, `run`, `report`). |

---

## 4. Como Executar

### Pré-requisito: Subir o SonarQube Server
Para rodar uma instância local do SonarQube via Docker:
```powershell
docker compose -f docker-compose.sonar.yml up -d
```
*Acesse o painel web em:* [http://localhost:9000](http://localhost:9000) *(usuário inicial: `admin` / senha: `admin`)*.

### Passo 1: Executar a Análise do SonarQube
Execute o script que orquestra o `dotnet-sonarscanner`, compila o projeto e roda os testes com coleta de cobertura:
```powershell
powershell -File scripts/run-sonar.ps1
```

Se o SonarQube exigir token de autenticação, defina a variável de ambiente antes de executar:
```powershell
$env:SONAR_TOKEN = "seu-token-gerado-no-sonar"
powershell -File scripts/run-sonar.ps1
```

### Passo 2: Executar o Quality Gate do Solidify
Com a análise estática concluída no SonarQube, execute o Solidify para processar o Quality Gate completo e auditar os princípios SOLID:
```powershell
powershell -File scripts/solidify-audit.ps1 -Profile quick
```

Perfis disponíveis:
* `quick` (Score mínimo: 70): verificação rápida de desenvolvimento/PR.
* `release` (Score mínimo: 75): validação formal de release com múltiplos revisores e relatório consolidado.
* `contractual` (Score mínimo: 85): auditoria rigorosa com modelos externos e certificação formal em PDF.

### Passo 3: Visualizar os Resultados
* **Dashboard do SonarQube**: [http://localhost:9000/dashboard?id=desafio_tecnico_Verity](http://localhost:9000/dashboard?id=desafio_tecnico_Verity)
* **Laudo do Solidify**: gerado em `.solidify/out/release-report.json` e visualizável via comando `solidify dashboard`.
