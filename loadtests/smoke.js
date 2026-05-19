/**
 * k6 Smoke Test — runs on every CI push
 * 1 VU, 30 s — verifies the app is alive and key endpoints respond correctly.
 *
 * Usage:
 *   k6 run loadtests/smoke.js
 *   k6 run --env BASE_URL=http://localhost:8080 loadtests/smoke.js
 */
import http from 'k6/http';
import { check, sleep } from 'k6';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:8080';

export const options = {
  vus: 1,
  duration: '30s',
  thresholds: {
    // Zero tolerance for errors in smoke
    http_req_failed: ['rate<0.01'],
    // All responses under 500 ms
    http_req_duration: ['p(95)<500'],
  },
};

export default function () {
  // Health check
  const health = http.get(`${BASE_URL}/health`);
  check(health, {
    'health: status 200': (r) => r.status === 200,
    'health: body is Healthy': (r) => r.body.includes('Healthy'),
  });

  // Metrics endpoint (Prometheus)
  const metrics = http.get(`${BASE_URL}/metrics`);
  check(metrics, {
    'metrics: status 200': (r) => r.status === 200,
    'metrics: content is text': (r) => r.headers['Content-Type'].includes('text/plain'),
  });

  // Home page
  const home = http.get(`${BASE_URL}/`);
  check(home, {
    'home: status 200': (r) => r.status === 200,
    'home: contains DentaCare': (r) => r.body.includes('DentaCare'),
  });

  // Login page (Customer portal)
  const login = http.get(`${BASE_URL}/Account/Login`);
  check(login, {
    'login: status 200': (r) => r.status === 200,
    'login: has form': (r) => r.body.includes('pat-email'),
  });

  sleep(1);
}
