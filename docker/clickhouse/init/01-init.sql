-- Create database (already created by env var, but explicit is good)
CREATE DATABASE IF NOT EXISTS metrics;

-- Use the database
USE metrics;

-- Main metrics table
CREATE TABLE IF NOT EXISTS metrics (
    timestamp DateTime64(3) CODEC(Delta, ZSTD),
    metric_name LowCardinality(String),
    value Float64 CODEC(ZSTD),
    tags Map(String, String),
    -- Common tag fields extracted for better query performance
    host LowCardinality(String),
    service LowCardinality(String),
    environment LowCardinality(String)
) ENGINE = MergeTree()
PARTITION BY toYYYYMM(timestamp)
ORDER BY (metric_name, host, service, timestamp)
TTL timestamp + INTERVAL 90 DAY
SETTINGS index_granularity = 8192;

-- Materialized view for 1-minute aggregations (pre-computed for fast queries)
CREATE MATERIALIZED VIEW IF NOT EXISTS metrics_1m
ENGINE = AggregatingMergeTree()
PARTITION BY toYYYYMM(minute)
ORDER BY (metric_name, host, service, minute)
TTL minute + INTERVAL 180 DAY
AS SELECT
    toStartOfMinute(timestamp) as minute,
    metric_name,
    host,
    service,
    environment,
    avgState(value) as avg_value,
    maxState(value) as max_value,
    minState(value) as min_value,
    sumState(value) as sum_value,
    countState() as count
FROM metrics
GROUP BY minute, metric_name, host, service, environment;

-- Materialized view for 1-hour aggregations
CREATE MATERIALIZED VIEW IF NOT EXISTS metrics_1h
ENGINE = AggregatingMergeTree()
PARTITION BY toYYYYMM(hour)
ORDER BY (metric_name, host, service, hour)
TTL hour + INTERVAL 365 DAY
AS SELECT
    toStartOfHour(timestamp) as hour,
    metric_name,
    host,
    service,
    environment,
    avgState(value) as avg_value,
    maxState(value) as max_value,
    minState(value) as min_value,
    sumState(value) as sum_value,
    countState() as count
FROM metrics
GROUP BY hour, metric_name, host, service, environment;

-- Create user (if not using env vars)
-- Already handled by CLICKHOUSE_USER env var