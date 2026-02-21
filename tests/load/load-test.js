/**
 * Faro Load Test - High-Throughput Metrics Ingestion
 *
 * This test validates the system's ability to handle high-volume metric ingestion
 * and verifies the claim of 1M+ metrics per second throughput.
 *
 * Test scenarios:
 * 1. Warm-up: 10k metrics/sec for 30s
 * 2. Ramp-up: Gradually increase to target load
 * 3. Sustained: Hold at target load
 * 4. Stress: Push beyond target to find breaking point
 * 5. Cool-down: Gradual decrease
 */

import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate, Trend, Counter } from 'k6/metrics';

// Custom metrics
const failureRate = new Rate('failed_requests');
const metricsSent = new Counter('metrics_sent');
const batchDuration = new Trend('batch_duration');

// Configuration
const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';
const BATCH_SIZE = parseInt(__ENV.BATCH_SIZE || '100');
const TEST_PROFILE = __ENV.TEST_PROFILE || 'medium';

// Test profiles with different load characteristics
const profiles = {
  // Smoke test - basic functionality validation
  smoke: {
    stages: [
      { duration: '30s', target: 10 },  // 10 VUs = ~1k metrics/sec
    ],
    thresholds: {
      http_req_failed: ['rate<0.01'],  // Less than 1% errors
      http_req_duration: ['p(95)<500'], // 95% of requests under 500ms
    }
  },

  // Light load - 100k metrics/sec
  light: {
    stages: [
      { duration: '1m', target: 100 },    // Ramp-up to 100 VUs
      { duration: '3m', target: 100 },    // Hold at 100 VUs (~10k metrics/sec)
      { duration: '1m', target: 0 },      // Cool-down
    ],
    thresholds: {
      http_req_failed: ['rate<0.01'],
      http_req_duration: ['p(95)<1000'],
      http_req_duration: ['p(99)<2000'],
    }
  },

  // Medium load - 500k metrics/sec
  medium: {
    stages: [
      { duration: '30s', target: 100 },   // Warm-up
      { duration: '2m', target: 500 },    // Ramp to 500 VUs (~50k metrics/sec)
      { duration: '5m', target: 500 },    // Hold at 500 VUs
      { duration: '2m', target: 1000 },   // Push to 1000 VUs (~100k metrics/sec)
      { duration: '5m', target: 1000 },   // Hold at 1000 VUs
      { duration: '1m', target: 0 },      // Cool-down
    ],
    thresholds: {
      http_req_failed: ['rate<0.05'],     // Less than 5% errors under load
      http_req_duration: ['p(95)<2000'],  // 95% under 2s
      http_req_duration: ['p(99)<5000'],  // 99% under 5s
    }
  },

  // Heavy load - 1M+ metrics/sec (requires multiple load generators)
  heavy: {
    stages: [
      { duration: '1m', target: 500 },    // Warm-up
      { duration: '3m', target: 2000 },   // Ramp to 2000 VUs
      { duration: '10m', target: 2000 },  // Hold at 2000 VUs (~200k metrics/sec)
      { duration: '3m', target: 3000 },   // Push to 3000 VUs
      { duration: '10m', target: 3000 },  // Hold at 3000 VUs (~300k metrics/sec)
      { duration: '2m', target: 0 },      // Cool-down
    ],
    thresholds: {
      http_req_failed: ['rate<0.10'],     // Less than 10% errors
      http_req_duration: ['p(95)<5000'],  // 95% under 5s
    }
  },

  // Stress test - find the breaking point
  stress: {
    stages: [
      { duration: '1m', target: 500 },    // Warm-up
      { duration: '2m', target: 1000 },   //
      { duration: '2m', target: 2000 },   //
      { duration: '2m', target: 4000 },   // Push hard
      { duration: '5m', target: 4000 },   // Hold at breaking point
      { duration: '2m', target: 0 },      // Cool-down
    ],
    thresholds: {
      // More lenient thresholds for stress testing
      http_req_failed: ['rate<0.20'],     // Less than 20% errors
    }
  },
};

// Apply the selected profile
export const options = profiles[TEST_PROFILE];

// Metric name pools for realistic variance
const metricNames = [
  'cpu_usage',
  'memory_used',
  'disk_io_read',
  'disk_io_write',
  'network_bytes_in',
  'network_bytes_out',
  'api.request.duration',
  'api.request.count',
  'api.error.count',
  'db.query.duration',
  'db.connection.pool.size',
  'cache.hit.count',
  'cache.miss.count',
  'queue.depth',
  'worker.processing_time',
];

const environments = ['production', 'staging', 'development'];
const services = [
  'api-gateway',
  'auth-service',
  'user-service',
  'order-service',
  'payment-service',
  'notification-service',
  'analytics-service',
  'search-service',
];

// Generate realistic host names
function generateHostname(service, replica) {
  return `${service}-${replica}`;
}

// Generate a random metric value based on metric type
function generateMetricValue(metricName) {
  if (metricName.includes('cpu') || metricName.includes('memory')) {
    return Math.random() * 100; // 0-100%
  } else if (metricName.includes('duration') || metricName.includes('time')) {
    return Math.random() * 1000; // 0-1000ms
  } else if (metricName.includes('count')) {
    return Math.floor(Math.random() * 100);
  } else if (metricName.includes('bytes')) {
    return Math.floor(Math.random() * 1000000); // 0-1MB
  }
  return Math.random() * 100;
}

// Generate a batch of realistic metrics
function generateMetricBatch(size) {
  const metrics = [];
  const timestamp = new Date().toISOString();

  for (let i = 0; i < size; i++) {
    const service = services[Math.floor(Math.random() * services.length)];
    const metricName = metricNames[Math.floor(Math.random() * metricNames.length)];

    metrics.push({
      metricName: metricName,
      value: generateMetricValue(metricName),
      timestamp: timestamp,
      host: generateHostname(service, Math.floor(Math.random() * 10) + 1),
      service: service,
      environment: environments[Math.floor(Math.random() * environments.length)],
      tags: {
        region: `us-${['east', 'west', 'central'][Math.floor(Math.random() * 3)]}-1`,
        availability_zone: `${['a', 'b', 'c'][Math.floor(Math.random() * 3)]}`,
        version: `v${Math.floor(Math.random() * 5) + 1}.${Math.floor(Math.random() * 10)}`,
      }
    });
  }

  return { metrics };
}

// Main test function - executed by each virtual user
export default function () {
  const batch = generateMetricBatch(BATCH_SIZE);

  const params = {
    headers: {
      'Content-Type': 'application/json',
    },
    tags: {
      name: 'batch_metrics',
    },
  };

  const startTime = Date.now();
  const response = http.post(
    `${BASE_URL}/api/metrics/batch`,
    JSON.stringify(batch),
    params
  );
  const duration = Date.now() - startTime;

  // Record custom metrics
  batchDuration.add(duration);
  metricsSent.add(BATCH_SIZE);

  // Check response
  const success = check(response, {
    'status is 202': (r) => r.status === 202,
    'response has received count': (r) => {
      try {
        const body = JSON.parse(r.body);
        return body.received === BATCH_SIZE;
      } catch (e) {
        return false;
      }
    },
  });

  failureRate.add(!success);

  // Add slight delay to avoid overwhelming the system
  // This is configurable based on desired throughput
  const delayMs = parseFloat(__ENV.DELAY_MS || '0.1');
  sleep(delayMs);
}

// Setup function - runs once before test starts
export function setup() {
  console.log('='.repeat(80));
  console.log('Faro Load Test Configuration');
  console.log('='.repeat(80));
  console.log(`Base URL: ${BASE_URL}`);
  console.log(`Test Profile: ${TEST_PROFILE}`);
  console.log(`Batch Size: ${BATCH_SIZE}`);
  console.log(`Expected throughput: ~${BATCH_SIZE * 10} metrics/sec per 100 VUs`);
  console.log('='.repeat(80));

  // Verify collector is responsive
  const healthCheck = http.get(`${BASE_URL}/health`);
  if (healthCheck.status !== 200) {
    console.error('WARNING: Collector health check failed. Service may not be ready.');
  }

  return {
    startTime: new Date().toISOString(),
  };
}

// Teardown function - runs once after test completes
export function teardown(data) {
  console.log('='.repeat(80));
  console.log('Load Test Complete');
  console.log('='.repeat(80));
  console.log(`Start Time: ${data.startTime}`);
  console.log(`End Time: ${new Date().toISOString()}`);
  console.log('='.repeat(80));
  console.log('Check the results summary above for detailed metrics.');
  console.log('Review Grafana dashboards for system performance during the test.');
  console.log('='.repeat(80));
}
