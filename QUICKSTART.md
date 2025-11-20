## Quickstart Guide

Get this entire project running in under 5 minutes! ⏱️

## Prerequisites

**Required:**
- add here

## Initial steps

# From project root
docker-compose up -d

# Verify ClickHouse is running
docker-compose ps

# Check logs
docker-compose logs clickhouse

# Connect to ClickHouse CLI (optional, for testing)
docker exec -it faro-clickhouse clickhouse-client --user metrics_user --password metrics_pass_123 --database metrics