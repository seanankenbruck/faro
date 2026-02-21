#!/bin/bash

#######################################
# System Resource Monitor
#
# Monitors Docker containers and system resources during load test
#######################################

# Colors
BLUE='\033[0;34m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

INTERVAL=${1:-5}  # Default 5 seconds

echo ""
echo -e "${BLUE}========================================${NC}"
echo -e "${BLUE}Faro System Resource Monitor${NC}"
echo -e "${BLUE}Refresh interval: ${INTERVAL}s${NC}"
echo -e "${BLUE}========================================${NC}"
echo ""
echo -e "${YELLOW}Press Ctrl+C to stop${NC}"
echo ""

while true; do
    clear
    echo -e "${BLUE}=== System Resources $(date '+%Y-%m-%d %H:%M:%S') ===${NC}"
    echo ""

    # Docker container stats
    echo -e "${GREEN}Docker Container Stats:${NC}"
    docker stats --no-stream --format "table {{.Name}}\t{{.CPUPerc}}\t{{.MemUsage}}\t{{.NetIO}}\t{{.BlockIO}}" \
        faro-collector faro-consumer faro-kafka faro-clickhouse 2>/dev/null || echo "No containers running"

    echo ""

    # ClickHouse metrics count
    echo -e "${GREEN}ClickHouse Metrics:${NC}"
    METRICS_COUNT=$(curl -s "http://localhost:8123/?query=SELECT+count(*)+FROM+metrics" 2>/dev/null || echo "N/A")
    echo "Total metrics: ${METRICS_COUNT}"

    # Recent ingestion rate
    RECENT_1M=$(curl -s "http://localhost:8123/" --data "SELECT count(*) FROM metrics WHERE timestamp >= now() - INTERVAL 1 MINUTE" 2>/dev/null || echo "N/A")
    echo "Last 1 minute: ${RECENT_1M}"

    if [ "$RECENT_1M" != "N/A" ] && [ "$RECENT_1M" -gt 0 ]; then
        RATE=$((RECENT_1M / 60))
        echo -e "Ingestion rate: ~${RATE} metrics/sec"
    fi

    echo ""

    # Kafka lag
    echo -e "${GREEN}Kafka Consumer Lag:${NC}"
    docker exec faro-kafka kafka-consumer-groups \
        --bootstrap-server localhost:9092 \
        --group faro-consumer-group \
        --describe 2>/dev/null | grep faro-metrics || echo "No consumer group found"

    echo ""
    echo -e "${YELLOW}Refreshing in ${INTERVAL}s...${NC}"

    sleep $INTERVAL
done
