## Quickstart Guide

Get this entire project running in under 5 minutes! ⏱️

## Prerequisites

**Required:**
- add here

## Initial steps

# Start ClickHouse
docker-compose up -d

# Verify ClickHouse is running
docker-compose ps

# Check logs
docker-compose logs clickhouse

# Connect to ClickHouse CLI (optional, for testing)
docker exec -it faro-clickhouse clickhouse-client --user metrics_user --password metrics_pass_123 --database metrics

# Terminal 1: Start ClickHouse
docker-compose up -d

# Terminal 2: Start Collector
cd src/MetricsMonitoring.Collector
dotnet run

# Terminal 3: Run Test App
cd src/TestApp
dotnet run

# Connect to ClickHouse
docker exec -it metrics-clickhouse clickhouse-client --user metrics_user --password metrics_pass_123 --database metrics

# Run queries
```
SELECT count() FROM metrics;

SELECT 
    metric_name, 
    count() as cnt, 
    avg(value) as avg_value,
    min(value) as min_value,
    max(value) as max_value
FROM metrics 
GROUP BY metric_name;

SELECT 
    metric_name,
    host,
    toStartOfMinute(timestamp) as minute,
    avg(value) as avg_value
FROM metrics
WHERE metric_name = 'cpu.usage'
GROUP BY metric_name, host, minute
ORDER BY minute DESC
LIMIT 10;
```

# Test health check endpoint
`curl http://localhost:5000/api/health`

# Test Swagger UI

Navigate to: `http://localhost:5000/swagger`