# 🚀 Testes de Carga e Performance com K6 & Solidify Quality Gate

Este diretório contém os scripts de testes de carga, stress e validação de performance desenvolvidos com **[Grafana k6](https://k6.io/)** e integrados nativamente ao **Solidify Release Quality Gate**.

---

## 🎯 1. Objetivos da Engenharia de Performance

1. **Garantir SLAs Corporativos:** Assegurar que os microsserviços `Transactions.Api` e `Consolidation.Api` operem dentro dos limites rigorosos de latência sob concorrência.
2. **Validar o Transactional Outbox sob Carga:** Simular múltiplos usuários virtuais concorrentes gerando lançamentos financeiros simultâneos e garantir que a fila do RabbitMQ e a consolidação assíncrona absorvam o throughput sem perdas ou deadlocks.
3. **Alimentar o Quality Gate do Solidify:** O analisador de carga do Solidify (`analyzers.load`) executa o script do k6, normaliza as métricas (P95, P99, Throughput, Error Rate) e atribui nota ao pilar de **Performance** no cálculo do Score Final de Release.

---

## 📊 2. Critérios de Aceitação e Thresholds

Os testes possuem limites de aceitação (*Thresholds*) definidos no script e espelhados no [`solidify.json`](../../solidify.json):

| Métrica | Limite de Aceitação (Threshold) | Objetivo |
|---|---|---|
| **P95 Latência** (`http_req_duration: p(95)`) | `< 500 ms` | 95% das requisições devem responder em menos de 0.5s. |
| **P99 Latência** (`http_req_duration: p(99)`) | `< 1000 ms` | 99% das requisições devem responder em menos de 1.0s. |
| **Taxa de Falha HTTP** (`http_req_failed`) | `< 0.01` (1%) | Erros 4xx/5xx não tolerados acima de 1%. |
| **Sucesso de Transações** (`transaction_success_rate`)| `> 0.99` (99%) | Confirmação de 201 Created em lançamentos. |

---

## 🧪 3. Scripts Disponíveis

### A. Teste de Carga Completo (`k6-load-test.js`)
* **Duração:** 30 segundos em 3 estágios (`5s ramp-up` -> `20s platô com 10 VUs` -> `5s ramp-down`).
* **Cenários testados por iteração:**
  1. `01_Healthcheck`: `GET /health` em ambas as APIs.
  2. `02_Register_Transaction`: `POST /api/v1/transactions` alternando 70% créditos e 30% débitos aleatórios.
  3. `03_Query_Transactions_And_Balance`: `GET /api/v1/transactions` e `GET /api/v1/daily-balances`.
* **Uso:** Script padrão acionado pelo Solidify no perfil `quick` e `release`.

### B. Smoke Test Rápido (`k6-smoke-test.js`)
* **Duração:** 10 segundos com 2 VUs constantes.
* **Objetivo:** Validação de sanidade ultra-rápida pré-commit ou em pipelines de pull request.

---

## 💻 4. Como Executar Localmente

### Pré-requisitos
Ter os containers da aplicação rodando via Docker:
```bash
docker compose up -d
```

### Executar via binário local do K6:
```powershell
# Executar Smoke Test (10s, 2 VUs)
k6 run tests/load/k6-smoke-test.js

# Executar Teste de Carga Completo (30s, 10 VUs)
k6 run tests/load/k6-load-test.js
```

### Executar via Docker (sem instalar k6 no host):
```bash
docker run --rm -i --add-host=host.docker.internal:host-gateway -v ${PWD}/tests/load:/scripts grafana/k6 run /scripts/k6-load-test.js -e TRANSACTIONS_URL=http://host.docker.internal:5101 -e CONSOLIDATION_URL=http://host.docker.internal:5102
```

---

## 🛡️ 5. Integração com o Solidify Quality Gate

No arquivo [`solidify.json`](../../solidify.json), a seção `analyzers.load` e `targets.api` estão configuradas:

```json
{
  "targets": {
    "api": [
      "http://localhost:5101",
      "http://localhost:5102"
    ]
  },
  "analyzers": {
    "load": {
      "enabled": true,
      "default_mode": "smoke",
      "mode": "binary",
      "require_thresholds_for_score": true,
      "script_path": "tests/load/k6-load-test.js",
      "vus": 10,
      "duration_seconds": 30,
      "threshold_p95_ms": 500,
      "threshold_p99_ms": 1000,
      "max_error_rate": 0.01,
      "allow_prod": false
    }
  }
}
```

Ao executar o Solidify:
```powershell
.\.agents\skills\solidify\bin\solidify.cmd run --profile quick --dir .
```

O Solidify executa o k6 em segundo plano, valida os percentis no JSON exportado e atribui `Score: 100` caso todos os thresholds sejam respeitados.
