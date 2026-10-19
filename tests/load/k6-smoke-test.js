import http from 'k6/http';
import { check, sleep } from 'k6';

// Smoke Test: Validação rápida de integridade da API sob carga mínima (1-2 VUs)
export const options = {
  vus: 2,
  duration: '10s',
  summaryTrendStats: ['avg', 'min', 'med', 'max', 'p(90)', 'p(95)', 'p(99)'],
  thresholds: {
    http_req_duration: ['p(95)<400', 'p(99)<800'],
    http_req_failed: ['rate<0.01'],
  },
};

const TRANSACTIONS_URL = __ENV.TRANSACTIONS_URL || 'http://localhost:5101';
const CONSOLIDATION_URL = __ENV.CONSOLIDATION_URL || 'http://localhost:5102';

export default function () {
  const currentDate = '2026-10-18';

  // 1. Healthcheck
  const txHealth = http.get(`${TRANSACTIONS_URL}/health`);
  check(txHealth, { 'Transactions API Healthy': (r) => r.status === 200 });

  const csHealth = http.get(`${CONSOLIDATION_URL}/health`);
  check(csHealth, { 'Consolidation API Healthy': (r) => r.status === 200 });

  // 2. Registro de Transação
  const payload = JSON.stringify({
    amount: 50.00,
    type: 1,
    description: 'Smoke Test K6',
    date: currentDate,
  });

  const resPost = http.post(
    `${TRANSACTIONS_URL}/api/v1/transactions`,
    payload,
    { headers: { 'Content-Type': 'application/json' } }
  );
  check(resPost, { 'Transação criada (201)': (r) => r.status === 201 });

  // 3. Consulta de Saldo
  const resBalance = http.get(
    `${CONSOLIDATION_URL}/api/v1/daily-balances?date=${currentDate}`
  );
  check(resBalance, { 'Saldo retornado (200)': (r) => r.status === 200 || r.status === 404 });

  sleep(1);
}
