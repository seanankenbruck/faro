# Load Testing Quick Start

## TL;DR - Run Your First Test in 60 Seconds

```bash
# 1. Install k6 (macOS)
brew install k6

# 2. Start Faro services
cd /path/to/faro
docker-compose up -d

# 3. Run smoke test
cd tests/load
./run-load-test.sh smoke

# 4. Verify results
./verify-ingestion.sh
```

## Commands Cheat Sheet

### Running Tests

```bash
# Smoke test (30s) - Always run this first!
./run-load-test.sh smoke

# Light load (5min, ~10k metrics/sec)
./run-load-test.sh light

# Medium load (15min, ~100k metrics/sec)
./run-load-test.sh medium

# Heavy load (30min, ~300k metrics/sec)
./run-load-test.sh heavy

# Stress test (find breaking point)
./run-load-test.sh stress

# Custom configuration
BASE_URL=http://staging:5000 BATCH_SIZE=500 ./run-load-test.sh medium
```

### Monitoring

```bash
# Watch metrics count in real-time
watch -n 1 'curl -s "http://localhost:8123/?query=SELECT+count(*)+FROM+metrics"'

# Monitor system resources
./monitor-system.sh

# View Grafana dashboards
open http://localhost:3000

# View Kafka UI
open http://localhost:8080
```

### Verification

```bash
# Verify metrics reached ClickHouse
./verify-ingestion.sh

# Check specific metrics
curl "http://localhost:8123/" --data "SELECT * FROM metrics LIMIT 10 FORMAT Pretty"

# Check ingestion rate
curl "http://localhost:8123/" --data "
SELECT
  toStartOfMinute(timestamp) as minute,
  count() as metrics_count
FROM metrics
WHERE timestamp >= now() - INTERVAL 10 MINUTE
GROUP BY minute
ORDER BY minute DESC
FORMAT PrettyCompact
"
```

### Troubleshooting

```bash
# Check if services are running
docker-compose ps

# View collector logs
docker-compose logs collector --tail=50 -f

# View consumer logs
docker-compose logs consumer --tail=50 -f

# Check Kafka consumer lag
docker exec faro-kafka kafka-consumer-groups \
  --bootstrap-server localhost:9092 \
  --group faro-consumer-group \
  --describe

# Restart services
docker-compose restart collector consumer
```

## Recommended Test Progression

### First Time Users

1. **Smoke test** (30s) - Verify everything works
   ```bash
   ./run-load-test.sh smoke
   ```

2. **Verify ingestion**
   ```bash
   ./verify-ingestion.sh
   ```

3. **Light test** (5min) - Build confidence
   ```bash
   ./run-load-test.sh light
   ```

### Performance Validation

4. **Medium test** (15min) - Realistic production load
   ```bash
   ./run-load-test.sh medium
   ```

5. **Heavy test** (30min) - Peak load
   ```bash
   ./run-load-test.sh heavy
   ```

### Finding Limits

6. **Stress test** (14min) - Find breaking point
   ```bash
   ./run-load-test.sh stress
   ```

## Expected Results

| Test | Duration | VUs | Expected Throughput | P95 Latency | Failure Rate |
|------|----------|-----|---------------------|-------------|--------------|
| smoke | 30s | 10 | ~1k/sec | <500ms | <1% |
| light | 5m | 100 | ~10k/sec | <1s | <1% |
| medium | 15m | 1000 | ~100k/sec | <2s | <5% |
| heavy | 30m | 3000 | ~300k/sec | <5s | <10% |

## Understanding k6 Output

```
     ✓ status is 202
     ✓ response has received count

     checks.........................: 100.00% ✓ 5000      ✗ 0
     data_received..................: 1.2 MB  40 kB/s
     data_sent......................: 15 MB   500 kB/s
     http_req_blocked...............: avg=1.5ms    p(95)=4.2ms
     http_req_duration..............: avg=234ms    p(95)=456ms   ← Key metric
     http_req_failed................: 0.00%   ✓ 0        ✗ 5000   ← Key metric
     http_reqs......................: 5000    166/s
     metrics_sent...................: 500000  16666/s              ← Actual throughput
```

**Key Metrics:**
- **http_req_failed**: Should be <5% for medium load
- **http_req_duration p95**: Should be <2s for medium load
- **metrics_sent**: Total metrics ingested

## Common Issues

### "Connection refused"
**Solution:** Start services with `docker-compose up -d`

### "High failure rate"
**Solution:** Reduce load or increase batch size
```bash
BATCH_SIZE=500 ./run-load-test.sh light
```

### "Metrics not in ClickHouse"
**Solution:** Check consumer is running
```bash
docker-compose logs consumer
```

### "Out of memory"
**Solution:** Increase Docker memory limit or reduce VUs

## Need Help?

- See [README.md](README.md) for comprehensive documentation
- Run interactive session: `./example-test-session.sh`
- Check logs: `docker-compose logs collector consumer`
