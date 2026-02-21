# Faro Load Testing Suite

This directory contains comprehensive load tests for validating Faro's high-throughput metrics ingestion capabilities.

## Overview

The load testing suite uses [k6](https://k6.io/) to simulate realistic production workloads and validate the system's ability to handle 1M+ metrics per second.

## Prerequisites

### Install k6

**macOS:**
```bash
brew install k6
```

**Linux (Debian/Ubuntu):**
```bash
sudo gpg -k
sudo gpg --no-default-keyring --keyring /usr/share/keyrings/k6-archive-keyring.gpg --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys C5AD17C747E3415A3642D57D77C6C491D6AC1D69
echo "deb [signed-by=/usr/share/keyrings/k6-archive-keyring.gpg] https://dl.k6.io/deb stable main" | sudo tee /etc/apt/sources.list.d/k6.list
sudo apt-get update
sudo apt-get install k6
```

**Windows:**
```powershell
choco install k6
```

Or download from: https://k6.io/docs/getting-started/installation/

### Start Faro Services

Before running load tests, ensure all services are running:

```bash
# From the project root
docker-compose up -d

# Verify services are healthy
docker-compose ps
```

## Quick Start

### Run a Smoke Test (30 seconds)

```bash
cd tests/load
chmod +x run-load-test.sh
./run-load-test.sh smoke
```

### Run a Medium Load Test (15 minutes)

```bash
./run-load-test.sh medium
```

## Test Profiles

### 1. Smoke Test (`smoke`)
- **Duration:** 30 seconds
- **Load:** 10 VUs (Virtual Users)
- **Expected Throughput:** ~1,000 metrics/second
- **Purpose:** Basic functionality validation
- **Use Case:** Quick verification that the system works

```bash
./run-load-test.sh smoke
```

### 2. Light Load (`light`)
- **Duration:** 5 minutes
- **Load:** 100 VUs
- **Expected Throughput:** ~10,000 metrics/second
- **Purpose:** Validate system under light production load
- **Use Case:** Development environment testing

```bash
./run-load-test.sh light
```

### 3. Medium Load (`medium`)
- **Duration:** 15 minutes
- **Load:** 500-1000 VUs
- **Expected Throughput:** ~50,000-100,000 metrics/second
- **Purpose:** Validate system under typical production load
- **Use Case:** Staging environment performance testing

```bash
./run-load-test.sh medium
```

### 4. Heavy Load (`heavy`)
- **Duration:** 30 minutes
- **Load:** 2000-3000 VUs
- **Expected Throughput:** ~200,000-300,000 metrics/second
- **Purpose:** Validate system under peak production load
- **Use Case:** Pre-production capacity planning

```bash
./run-load-test.sh heavy
```

### 5. Stress Test (`stress`)
- **Duration:** 14 minutes
- **Load:** Up to 4000 VUs
- **Expected Throughput:** ~400,000+ metrics/second
- **Purpose:** Find the breaking point
- **Use Case:** Capacity planning and failure mode testing

```bash
./run-load-test.sh stress
```

## Configuration

### Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `BASE_URL` | `http://localhost:5000` | Collector API endpoint |
| `BATCH_SIZE` | `100` | Number of metrics per batch request |
| `DELAY_MS` | `0.1` | Delay between requests (milliseconds) |
| `TEST_PROFILE` | `medium` | Test profile to run |

### Custom Test Configuration

```bash
# Test against a different endpoint
BASE_URL=http://staging.example.com:5000 ./run-load-test.sh medium

# Use larger batches for more efficient ingestion
BATCH_SIZE=500 ./run-load-test.sh heavy

# Adjust delay to control throughput
DELAY_MS=1 ./run-load-test.sh medium
```

## Monitoring During Tests

### Real-Time Metrics

#### Watch ClickHouse Metric Count
```bash
watch -n 1 'curl -s "http://localhost:8123/?query=SELECT+count(*)+FROM+metrics"'
```

#### Check Kafka Lag
```bash
# Open Kafka UI
open http://localhost:8080
```

#### View Grafana Dashboards
```bash
# Open Grafana
open http://localhost:3000
# Login: admin / admin
```

### k6 Metrics

During the test, k6 outputs real-time metrics:

- **http_req_duration:** Request latency (p95, p99)
- **http_req_failed:** Failed request rate
- **http_reqs:** Requests per second
- **metrics_sent:** Custom counter of metrics ingested
- **batch_duration:** Custom metric for batch processing time

## Distributed Load Testing

To achieve 1M+ metrics per second, you may need to run k6 from multiple machines:

### Using k6 Cloud (Recommended for High Load)

```bash
# Sign up at https://k6.io/cloud
k6 login cloud

# Run distributed test
k6 cloud load-test.js \
    -e BASE_URL=http://your-collector:5000 \
    -e TEST_PROFILE=heavy
```

### Manual Distribution (Multiple k6 Instances)

Run the same test from multiple machines simultaneously:

```bash
# Machine 1
BASE_URL=http://collector:5000 ./run-load-test.sh heavy &

# Machine 2
BASE_URL=http://collector:5000 ./run-load-test.sh heavy &

# Machine 3
BASE_URL=http://collector:5000 ./run-load-test.sh heavy &
```

Aggregate results manually or use a shared results database.

## Understanding Results

### Success Criteria

#### Smoke Test
- ✅ `http_req_failed < 1%`
- ✅ `http_req_duration p95 < 500ms`

#### Light Load
- ✅ `http_req_failed < 1%`
- ✅ `http_req_duration p95 < 1s`
- ✅ `http_req_duration p99 < 2s`

#### Medium Load
- ✅ `http_req_failed < 5%`
- ✅ `http_req_duration p95 < 2s`
- ✅ `http_req_duration p99 < 5s`

#### Heavy/Stress Load
- ✅ `http_req_failed < 10%`
- ✅ System remains stable (no crashes)
- ✅ Metrics eventually reach ClickHouse

### Output Files

After each test, results are saved to `./results/YYYYMMDD_HHMMSS/`:

- **summary.json:** High-level test results and metrics
- **results.json:** Detailed per-request data
- **test.log:** Complete k6 output log

### Interpreting Results

```json
{
  "metrics": {
    "http_req_duration": {
      "avg": 234.5,
      "p95": 450.2,
      "p99": 890.1
    },
    "http_req_failed": {
      "rate": 0.023  // 2.3% failure rate
    },
    "metrics_sent": {
      "count": 5000000  // Total metrics sent
    }
  }
}
```

**Key Indicators:**
- **Low failure rate (< 5%):** System is handling load well
- **P95 latency < 2s:** Collector is responsive under load
- **Increasing latency over time:** System may be saturating
- **High failure rate (> 10%):** System is overloaded

## Validating ClickHouse Ingestion

After the load test completes, verify metrics reached ClickHouse:

```bash
# Check total count
curl "http://localhost:8123/?query=SELECT+count(*)+FROM+metrics"

# Check recent metrics (last 5 minutes)
curl "http://localhost:8123/" --data "SELECT count(*) FROM metrics WHERE timestamp >= now() - INTERVAL 5 MINUTE"

# Check metrics by service
curl "http://localhost:8123/" --data "SELECT service, count(*) FROM metrics GROUP BY service ORDER BY count() DESC"

# Check ingestion rate over time
curl "http://localhost:8123/" --data "
SELECT
  toStartOfMinute(timestamp) as minute,
  count() as metrics_count
FROM metrics
WHERE timestamp >= now() - INTERVAL 1 HOUR
GROUP BY minute
ORDER BY minute DESC
LIMIT 10
FORMAT PrettyCompact
"
```

## Troubleshooting

### Issue: Connection Refused

```
Error: dial tcp 127.0.0.1:5000: connect: connection refused
```

**Solution:** Ensure the Collector is running:
```bash
docker-compose ps
docker-compose logs collector
```

### Issue: High Failure Rate

If you see > 10% failures during medium/heavy tests:

1. **Check Kafka lag:** Kafka may be backlogged
   ```bash
   docker-compose logs kafka | tail -50
   ```

2. **Check Collector logs:**
   ```bash
   docker-compose logs collector --tail=100 -f
   ```

3. **Increase Collector resources:**
   ```yaml
   # In docker-compose.yml
   collector:
     deploy:
       resources:
         limits:
           cpus: '4'
           memory: 4G
   ```

4. **Tune batch sizes:**
   ```bash
   # Larger batches = fewer requests
   BATCH_SIZE=500 ./run-load-test.sh medium
   ```

### Issue: ClickHouse Not Receiving Metrics

If metrics are sent but don't appear in ClickHouse:

1. **Check Consumer is running:**
   ```bash
   docker-compose ps consumer
   docker-compose logs consumer --tail=50
   ```

2. **Check Kafka messages:**
   - Open Kafka UI: http://localhost:8080
   - Check `faro-metrics` topic for messages

3. **Check Consumer lag:**
   ```bash
   docker-compose exec kafka kafka-consumer-groups \
     --bootstrap-server localhost:9092 \
     --group faro-consumer-group \
     --describe
   ```

### Issue: Out of Memory

If k6 or the Collector runs out of memory:

1. **Reduce VUs or batch size:**
   ```bash
   BATCH_SIZE=50 ./run-load-test.sh light
   ```

2. **Increase Docker memory limits:**
   ```bash
   # Docker Desktop: Settings → Resources → Memory
   ```

3. **Use distributed load testing** (see above)

## Performance Tuning Tips

### Collector Tuning

Edit `src/Faro.Collector/appsettings.json`:

```json
{
  "MetricsCollector": {
    "FlushIntervalSeconds": 1,  // Reduce for faster flushing
    "BatchSize": 5000            // Increase for larger Kafka batches
  },
  "Kafka": {
    "ProducerConfig": {
      "LingerMs": 10,           // Reduce for lower latency
      "BatchSize": 65536,       // Increase for better compression
      "CompressionType": "lz4"  // lz4 is faster than snappy
    }
  }
}
```

### ClickHouse Tuning

For high-throughput ingestion:

```sql
-- Increase max insert threads
SET max_insert_threads = 8;

-- Disable fsync for better performance (staging only!)
SET fsync_metadata = 0;
```

### Consumer Tuning

Edit `src/Faro.Consumer/appsettings.json`:

```json
{
  "Consumer": {
    "BatchSize": 10000,          // Larger batches to ClickHouse
    "BatchTimeoutSeconds": 5,    // Reduce for lower latency
    "MaxRetries": 3
  }
}
```

## Contributing

To add new test scenarios:

1. Edit `load-test.js` and add a new profile to the `profiles` object
2. Update this README with the new profile description
3. Test your changes with `./run-load-test.sh your-profile`

## References

- [k6 Documentation](https://k6.io/docs/)
- [k6 Best Practices](https://k6.io/docs/testing-guides/test-types/)
- [ClickHouse Performance Tips](https://clickhouse.com/docs/en/operations/tips/)
- [Kafka Performance Tuning](https://kafka.apache.org/documentation/#producerconfigs)
