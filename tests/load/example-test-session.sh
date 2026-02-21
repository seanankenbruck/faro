#!/bin/bash

#######################################
# Example Test Session
#
# This script demonstrates a complete load testing workflow
# with monitoring and validation
#######################################

set -e

# Colors
BLUE='\033[0;34m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m'

print_header() {
    echo ""
    echo -e "${BLUE}========================================${NC}"
    echo -e "${BLUE}$1${NC}"
    echo -e "${BLUE}========================================${NC}"
    echo ""
}

print_step() {
    echo -e "${GREEN}➜${NC} $1"
}

print_info() {
    echo -e "${YELLOW}ℹ${NC} $1"
}

print_header "Faro Load Testing - Complete Workflow Example"

echo "This script will guide you through a complete load testing session:"
echo ""
echo "1. Check prerequisites"
echo "2. Start monitoring (in background)"
echo "3. Get baseline metrics"
echo "4. Run load test"
echo "5. Verify ingestion"
echo "6. Generate report"
echo ""
echo -e "${YELLOW}Press Enter to continue, or Ctrl+C to cancel...${NC}"
read

# Step 1: Check prerequisites
print_header "Step 1: Checking Prerequisites"

print_step "Checking if k6 is installed..."
if command -v k6 &> /dev/null; then
    echo -e "${GREEN}✓${NC} k6 is installed: $(k6 version)"
else
    echo -e "${RED}✗${NC} k6 is not installed"
    echo ""
    echo "Install k6:"
    echo "  macOS:  brew install k6"
    echo "  Linux:  See https://k6.io/docs/getting-started/installation/"
    exit 1
fi

print_step "Checking if Docker services are running..."
if docker ps | grep -q "faro-collector"; then
    echo -e "${GREEN}✓${NC} Collector is running"
else
    echo -e "${RED}✗${NC} Collector is not running"
    echo ""
    echo "Start services with: docker-compose up -d"
    exit 1
fi

if docker ps | grep -q "faro-kafka"; then
    echo -e "${GREEN}✓${NC} Kafka is running"
else
    echo -e "${YELLOW}⚠${NC} Kafka is not running"
fi

if docker ps | grep -q "faro-clickhouse"; then
    echo -e "${GREEN}✓${NC} ClickHouse is running"
else
    echo -e "${YELLOW}⚠${NC} ClickHouse is not running"
fi

sleep 2

# Step 2: Start monitoring
print_header "Step 2: Starting Monitoring"

print_info "You can monitor the test in real-time using these tools:"
echo ""
echo "  Terminal 1: ./monitor-system.sh"
echo "  Terminal 2: watch -n 1 'curl -s http://localhost:8123/?query=SELECT+count(*)+FROM+metrics'"
echo "  Browser:    http://localhost:3000 (Grafana)"
echo "  Browser:    http://localhost:8080 (Kafka UI)"
echo ""
print_info "These will run in the background during the test"
echo ""
echo -e "${YELLOW}Press Enter to continue...${NC}"
read

# Step 3: Get baseline
print_header "Step 3: Getting Baseline Metrics"

print_step "Querying current metric count..."
BASELINE=$(curl -s "http://localhost:8123/?query=SELECT+count(*)+FROM+metrics" 2>/dev/null || echo "0")
echo "Current metrics in ClickHouse: ${BASELINE}"
echo ""
sleep 2

# Step 4: Choose test profile
print_header "Step 4: Choosing Test Profile"

echo "Select a test profile:"
echo ""
echo "  1. smoke  - Quick 30s test (recommended first time)"
echo "  2. light  - 5 minute test at 10k metrics/sec"
echo "  3. medium - 15 minute test at 50-100k metrics/sec"
echo ""
echo -n "Enter choice (1-3): "
read choice

case $choice in
    1)
        PROFILE="smoke"
        ;;
    2)
        PROFILE="light"
        ;;
    3)
        PROFILE="medium"
        ;;
    *)
        echo "Invalid choice, defaulting to smoke"
        PROFILE="smoke"
        ;;
esac

echo ""
print_info "Running ${PROFILE} test profile"
sleep 2

# Step 5: Run the test
print_header "Step 5: Running Load Test"

print_step "Starting k6 load test..."
echo ""

./run-load-test.sh ${PROFILE}

TEST_EXIT_CODE=$?

if [ $TEST_EXIT_CODE -ne 0 ]; then
    echo ""
    echo -e "${RED}✗${NC} Load test failed with exit code ${TEST_EXIT_CODE}"
    echo ""
    echo "Check the logs above for errors"
    exit 1
fi

echo ""
print_step "Load test completed successfully!"
sleep 2

# Step 6: Verify ingestion
print_header "Step 6: Verifying Ingestion"

print_info "Waiting 10 seconds for metrics to flow through the pipeline..."
sleep 10

print_step "Running verification script..."
echo ""

./verify-ingestion.sh

# Step 7: Calculate results
print_header "Step 7: Calculating Results"

AFTER_COUNT=$(curl -s "http://localhost:8123/?query=SELECT+count(*)+FROM+metrics" 2>/dev/null || echo "0")
INGESTED=$((AFTER_COUNT - BASELINE))

echo "Baseline metrics:  ${BASELINE}"
echo "Current metrics:   ${AFTER_COUNT}"
echo "Metrics ingested:  ${INGESTED}"
echo ""

if [ $INGESTED -gt 0 ]; then
    echo -e "${GREEN}✓${NC} Successfully ingested ${INGESTED} metrics!"
else
    echo -e "${YELLOW}⚠${NC} No new metrics detected. Check the consumer logs:"
    echo "    docker-compose logs consumer --tail=50"
fi

# Step 8: Next steps
print_header "Complete! Next Steps"

echo "Your load test is complete. Here's what you can do next:"
echo ""
echo "1. View detailed results:"
echo "   - Check ./results/ directory for k6 output"
echo ""
echo "2. View metrics in Grafana:"
echo "   - Open http://localhost:3000 (admin/admin)"
echo "   - Create a dashboard with ClickHouse datasource"
echo ""
echo "3. Query ClickHouse directly:"
echo "   - Run: curl 'http://localhost:8123/' --data 'SELECT * FROM metrics LIMIT 10 FORMAT Pretty'"
echo ""
echo "4. Run more intensive tests:"
echo "   - ./run-load-test.sh heavy"
echo "   - ./run-load-test.sh stress"
echo ""
echo -e "${GREEN}Happy load testing!${NC}"
echo ""
