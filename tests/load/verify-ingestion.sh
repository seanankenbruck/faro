#!/bin/bash

#######################################
# Verify Metrics Ingestion
#
# This script validates that metrics from the load test
# successfully made it through the entire pipeline to ClickHouse
#######################################

set -e

# Colors
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

CLICKHOUSE_URL=${CLICKHOUSE_URL:-"http://localhost:8123"}

echo ""
echo -e "${BLUE}========================================${NC}"
echo -e "${BLUE}Faro Metrics Ingestion Verification${NC}"
echo -e "${BLUE}========================================${NC}"
echo ""

# Check ClickHouse connectivity
echo -e "${BLUE}[1/7]${NC} Checking ClickHouse connectivity..."
if curl -sf "${CLICKHOUSE_URL}/ping" > /dev/null 2>&1; then
    echo -e "${GREEN}✓${NC} ClickHouse is responding"
else
    echo -e "${YELLOW}✗${NC} ClickHouse is not responding at ${CLICKHOUSE_URL}"
    exit 1
fi

# Total metrics count
echo ""
echo -e "${BLUE}[2/7]${NC} Checking total metrics count..."
TOTAL_COUNT=$(curl -s "${CLICKHOUSE_URL}/?query=SELECT+count(*)+FROM+metrics" 2>/dev/null || echo "0")
echo -e "${GREEN}✓${NC} Total metrics in database: ${TOTAL_COUNT}"

# Recent metrics (last 5 minutes)
echo ""
echo -e "${BLUE}[3/7]${NC} Checking recent metrics (last 5 minutes)..."
RECENT_COUNT=$(curl -s "${CLICKHOUSE_URL}/" --data "SELECT count(*) FROM metrics WHERE timestamp >= now() - INTERVAL 5 MINUTE" 2>/dev/null || echo "0")
echo -e "${GREEN}✓${NC} Metrics ingested in last 5 minutes: ${RECENT_COUNT}"

if [ "$RECENT_COUNT" -gt 0 ]; then
    echo -e "${GREEN}✓${NC} Recent ingestion is working!"
else
    echo -e "${YELLOW}⚠${NC} No recent metrics found. Was a load test running?"
fi

# Metrics by service
echo ""
echo -e "${BLUE}[4/7]${NC} Checking metrics distribution by service..."
curl -s "${CLICKHOUSE_URL}/" --data "
SELECT
    service,
    count() as metric_count,
    min(timestamp) as earliest,
    max(timestamp) as latest
FROM metrics
GROUP BY service
ORDER BY metric_count DESC
LIMIT 10
FORMAT PrettyCompact
" 2>/dev/null

# Metrics by name
echo ""
echo -e "${BLUE}[5/7]${NC} Checking metrics distribution by name..."
curl -s "${CLICKHOUSE_URL}/" --data "
SELECT
    metric_name,
    count() as metric_count
FROM metrics
GROUP BY metric_name
ORDER BY metric_count DESC
LIMIT 10
FORMAT PrettyCompact
" 2>/dev/null

# Ingestion rate over last hour
echo ""
echo -e "${BLUE}[6/7]${NC} Checking ingestion rate over last hour..."
curl -s "${CLICKHOUSE_URL}/" --data "
SELECT
    toStartOfMinute(timestamp) as minute,
    count() as metrics_per_minute,
    round(count() / 60, 2) as metrics_per_second
FROM metrics
WHERE timestamp >= now() - INTERVAL 1 HOUR
GROUP BY minute
ORDER BY minute DESC
LIMIT 10
FORMAT PrettyCompact
" 2>/dev/null

# Check materialized views
echo ""
echo -e "${BLUE}[7/7]${NC} Checking materialized views..."

METRICS_1M_COUNT=$(curl -s "${CLICKHOUSE_URL}/" --data "SELECT count(*) FROM metrics_1m" 2>/dev/null || echo "0")
echo -e "${GREEN}✓${NC} metrics_1m view: ${METRICS_1M_COUNT} rows"

METRICS_1H_COUNT=$(curl -s "${CLICKHOUSE_URL}/" --data "SELECT count(*) FROM metrics_1h" 2>/dev/null || echo "0")
echo -e "${GREEN}✓${NC} metrics_1h view: ${METRICS_1H_COUNT} rows"

# Summary
echo ""
echo -e "${BLUE}========================================${NC}"
echo -e "${BLUE}Summary${NC}"
echo -e "${BLUE}========================================${NC}"
echo ""
echo "Total Metrics:     ${TOTAL_COUNT}"
echo "Recent (5m):       ${RECENT_COUNT}"
echo "1-min aggregates:  ${METRICS_1M_COUNT}"
echo "1-hour aggregates: ${METRICS_1H_COUNT}"
echo ""

# Calculate throughput if we have recent data
if [ "$RECENT_COUNT" -gt 0 ]; then
    THROUGHPUT=$((RECENT_COUNT / 300))  # 300 seconds = 5 minutes
    echo -e "${GREEN}✓${NC} Average throughput (last 5m): ~${THROUGHPUT} metrics/second"
fi

echo ""
echo -e "${GREEN}Verification complete!${NC}"
echo ""
