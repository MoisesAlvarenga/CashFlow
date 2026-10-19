import http from 'k6/http';
import { check, group, sleep } from 'k6';
import { Counter, Rate, Trend } from 'k6/metrics';

// ============================================================================
// Métricas Customizadas para Avaliação de Qualidade e Performance
// ============================================================================
export const transactionsCreated = new Counter('transactions_created');
export const balancesQueried = new Counter('balances_queried');
export const transactionSuccessRate = new Rate('transaction_success_rate');
export const transactionPostDuration = new Trend('transaction_post_duration');
export const balanceGetDuration = new Trend('balance_get_duration');

// ============================================================================
// Configurações de Carga e Limites de Aceitação (Thresholds - Solidify Gate)
// ============================================================================
export const options = {
  // Define os percentis requeridos pelo analisador de carga do Solidify
  summaryTrendStats: ['avg', 'min', 'med', 'max', 'p(90)', 'p(95)', 'p(99)'],
  
  // Critérios de Aceitação (SLA Corporativo / Performance Gate)
  thresholds: {
    // 95% das requisições devem responder em menos de 500ms
    http_req_duration: ['p(95)<500', 'p(99)<1000'],
    // Taxa de erro HTTP deve ser inferior a 1%
    http_req_failed: ['rate<0.01'],
    // Taxa de sucesso específica das transações deve ser superior a 99%
    transaction_success_rate: ['rate>0.99'],
  },

  // Simulação de Carga Realista (Ramp-up -> Platô -> Ramp-down)
  stages: [
    { duration: '5s', target: 5 },   // Aquecimento gradual: 0 a 5 usuários
    { duration: '20s', target: 10 }, // Carga nominal: 10 usuários simultâneos
    { duration: '5s', target: 0 },   // Resfriamento suave: 10 a 0 usuários
  ],
};

// ============================================================================
// Endpoints e Variáveis de Ambiente (Suporte Local e CI/Docker)
// ============================================================================
const TRANSACTIONS_BASE_URL = __ENV.TRANSACTIONS_URL || 'http://localhost:5101';
const CONSOLIDATION_BASE_URL = __ENV.CONSOLIDATION_URL || 'http://localhost:5102';

const JSON_HEADERS = {
  headers: {
    'Content-Type': 'application/json',
    'Accept': 'application/json',
  },
};

// ============================================================================
// Fluxo de Execução do Usuário Virtual (VU)
// ============================================================================
export default function () {
  const currentDate = '2026-10-18';

  // --------------------------------------------------------------------------
  // Grupo 1: Healthcheck das APIs
  // --------------------------------------------------------------------------
  group('01_Healthcheck', function () {
    const resTxHealth = http.get(`${TRANSACTIONS_BASE_URL}/health`);
    check(resTxHealth, {
      'Transactions.Api está Healthy (200)': (r) => r.status === 200,
    });

    const resConsolidationHealth = http.get(`${CONSOLIDATION_BASE_URL}/health`);
    check(resConsolidationHealth, {
      'Consolidation.Api está Healthy (200)': (r) => r.status === 200,
    });
  });

  sleep(0.2);

  // --------------------------------------------------------------------------
  // Grupo 2: Registro Transacional (Escrita de Crédito e Débito via Outbox)
  // --------------------------------------------------------------------------
  group('02_Register_Transaction', function () {
    // Alterna aleatoriamente entre Crédito (1) e Débito (2)
    const isCredit = Math.random() > 0.3; // 70% crédito, 30% débito
    const type = isCredit ? 1 : 2;
    const amount = parseFloat((Math.random() * 200 + 10).toFixed(2));
    const description = `K6 Load Test - ${isCredit ? 'Crédito' : 'Débito'} VU:${__VU} Iter:${__ITER}`;

    const payload = JSON.stringify({
      amount: amount,
      type: type,
      description: description,
      date: currentDate,
    });

    const resPost = http.post(
      `${TRANSACTIONS_BASE_URL}/api/v1/transactions`,
      payload,
      JSON_HEADERS
    );

    const postSuccess = check(resPost, {
      'Transação registrada com sucesso (201 Created)': (r) => r.status === 201,
      'Retornou TransactionId válido': (r) => {
        try {
          const body = JSON.parse(r.body);
          return body.transactionId !== undefined && body.transactionId.length > 0;
        } catch (e) {
          return false;
        }
      },
    });

    transactionSuccessRate.add(postSuccess);
    transactionPostDuration.add(resPost.timings.duration);

    if (postSuccess) {
      transactionsCreated.add(1);
    }
  });

  sleep(0.3);

  // --------------------------------------------------------------------------
  // Grupo 3: Consulta de Lançamentos e Saldo Diário Consolidado (Leitura)
  // --------------------------------------------------------------------------
  group('03_Query_Transactions_And_Balance', function () {
    // Consulta lista de lançamentos
    const resList = http.get(
      `${TRANSACTIONS_BASE_URL}/api/v1/transactions?date=${currentDate}`,
      JSON_HEADERS
    );
    check(resList, {
      'Consulta de lançamentos retornou 200 OK': (r) => r.status === 200,
    });

    // Consulta saldo diário consolidado
    const resBalance = http.get(
      `${CONSOLIDATION_BASE_URL}/api/v1/daily-balances?date=${currentDate}`,
      JSON_HEADERS
    );

    const balanceSuccess = check(resBalance, {
      'Consulta de saldo consolidado retornou 200 OK ou 404': (r) =>
        r.status === 200 || r.status === 404,
    });

    balanceGetDuration.add(resBalance.timings.duration);
    if (balanceSuccess) {
      balancesQueried.add(1);
    }
  });

  // Pausa realista entre iterações da mesma VU (0.5s a 1.0s)
  sleep(Math.random() * 0.5 + 0.5);
}
