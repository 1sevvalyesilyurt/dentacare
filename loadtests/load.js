/**
 * k6 Load Test — runs on release tags
 * Simulates realistic clinic traffic: ramp up → sustain → ramp down.
 *
 * Usage:
 *   k6 run loadtests/load.js
 *   k6 run --env BASE_URL=http://localhost:8080 loadtests/load.js
 *
 * Thresholds (SLOs):
 *   - Error rate < 1%
 *   - p95 response time < 1 s
 *   - p99 response time < 2 s
 */
import http from 'k6/http';
import { check, group, sleep } from 'k6';
import { Rate, Trend } from 'k6/metrics';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:8080';

const errorRate = new Rate('errors');
const loginDuration = new Trend('login_page_duration');

export const options = {
  stages: [
    { duration: '1m', target: 5  },   // warm-up
    { duration: '3m', target: 20 },   // sustain load
    { duration: '1m', target: 40 },   // peak spike
    { duration: '1m', target: 0  },   // ramp down
  ],
  thresholds: {
    http_req_failed:   ['rate<0.01'],       // < 1% errors
    http_req_duration: ['p(95)<1000', 'p(99)<2000'],
    errors:            ['rate<0.01'],
  },
};

export default function () {
  group('Public pages', () => {
    const home = http.get(`${BASE_URL}/`);
    check(home, { 'home 200': (r) => r.status === 200 });
    errorRate.add(home.status !== 200);
    sleep(0.5);

    const privacy = http.get(`${BASE_URL}/Home/Privacy`);
    check(privacy, { 'privacy 200': (r) => r.status === 200 });
    errorRate.add(privacy.status !== 200);
  });

  group('Auth pages', () => {
    const loginPage = http.get(`${BASE_URL}/Account/Login`);
    loginDuration.add(loginPage.timings.duration);
    check(loginPage, { 'login page 200': (r) => r.status === 200 });
    errorRate.add(loginPage.status !== 200);
    sleep(0.5);

    const regPage = http.get(`${BASE_URL}/Account/Register`);
    check(regPage, { 'register page 200': (r) => r.status === 200 });
    errorRate.add(regPage.status !== 200);
  });

  group('Health & Metrics', () => {
    const health = http.get(`${BASE_URL}/health`);
    check(health, { 'health Healthy': (r) => r.body.includes('Healthy') });
    errorRate.add(!health.body.includes('Healthy'));
  });

  sleep(1);
}
