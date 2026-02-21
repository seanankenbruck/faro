#!/bin/bash

#######################################
# Faro Load Test Runner
#
# This script orchestrates load tests against the Faro metrics system
# and provides monitoring, reporting, and validation capabilities.
#######################################

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
BASE_URL=${BASE_URL:-"http://localhost:5000"}
TEST_PROFILE=${1:-"smoke"}
BATCH_SIZE=${BATCH_SIZE:-100}
DELAY_MS=${DELAY_MS:-0.1}
OUTPUT_DIR="./results/$(date +%Y%m%d_%H%M%S)"

# Print colored output
print_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

print_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

print_header() {
    echo ""
    echo -e "${BLUE}========================================${NC}"
    echo -e "${BLUE}$1${NC}"
    echo -e "${BLUE}========================================${NC}"
    echo ""
}

# Check if k6 is installed
check_k6() {
    if ! command -v k6 &> /dev/null; then
        print_error "k6 is not installed"
        print_info "Install k6 from: https://k6.io/docs/getting-started/installation/"
        print_info "On macOS: brew install k6"
        print_info "On Linux: sudo gpg -k && sudo gpg --no-default-keyring --keyring /usr/share/keyrings/k6-archive-keyring.gpg --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys C5AD17C747E3415A3642D57D77C6C491D6AC1D69 && echo \"deb [signed-by=/usr/share/keyrings/k6-archive-keyring.gpg] https://dl.k6.io/deb stable main\" | sudo tee /etc/apt/sources.list.d/k6.list && sudo apt-get update && sudo apt-get install k6"
        exit 1
    fi
    print_success "k6 is installed ($(k6 version))"
}

# Check if services are running
check_services() {
    print_info "Checking if Faro services are running..."

    # Check Collector
    if curl -sf "${BASE_URL}/api/health" > /dev/null 2>&1; then
        print_success "Collector is responding at ${BASE_URL}"
    else
        print_error "Collector is not responding at ${BASE_URL}"
        print_info "Start services with: docker-compose up -d"
        exit 1
    fi

    # Check ClickHouse
    if curl -sf "http://localhost:8123/ping" > /dev/null 2>&1; then
        print_success "ClickHouse is responding"
    else
        print_warning "ClickHouse may not be responding (this is optional for collector tests)"
    fi

    # Check Kafka
    if docker ps | grep -q "faro-kafka"; then
        print_success "Kafka container is running"
    else
        print_warning "Kafka container is not running (this may cause issues)"
    fi
}

# Get baseline metrics from ClickHouse
get_baseline_metrics() {
    print_info "Getting baseline metrics from ClickHouse..."

    BASELINE_COUNT=$(curl -s "http://localhost:8123/?query=SELECT+count(*)+FROM+metrics" 2>/dev/null || echo "0")
    print_info "Current metric count in ClickHouse: ${BASELINE_COUNT}"

    return 0
}

# Run the load test
run_load_test() {
    print_header "Starting Load Test: ${TEST_PROFILE}"

    mkdir -p "$OUTPUT_DIR"

    print_info "Test Configuration:"
    print_info "  Profile: ${TEST_PROFILE}"
    print_info "  Base URL: ${BASE_URL}"
    print_info "  Batch Size: ${BATCH_SIZE}"
    print_info "  Delay: ${DELAY_MS}ms"
    print_info "  Output Directory: ${OUTPUT_DIR}"

    # Run k6 with configuration
    k6 run \
        --out json="${OUTPUT_DIR}/results.json" \
        --summary-export="${OUTPUT_DIR}/summary.json" \
        -e BASE_URL="${BASE_URL}" \
        -e TEST_PROFILE="${TEST_PROFILE}" \
        -e BATCH_SIZE="${BATCH_SIZE}" \
        -e DELAY_MS="${DELAY_MS}" \
        load-test.js | tee "${OUTPUT_DIR}/test.log"

    TEST_EXIT_CODE=${PIPESTATUS[0]}

    if [ $TEST_EXIT_CODE -eq 0 ]; then
        print_success "Load test completed successfully"
    else
        print_error "Load test failed with exit code ${TEST_EXIT_CODE}"
    fi

    return $TEST_EXIT_CODE
}

# Generate HTML report
generate_report() {
    print_info "Generating test report..."

    if [ -f "${OUTPUT_DIR}/summary.json" ]; then
        cat "${OUTPUT_DIR}/summary.json" | python3 -m json.tool > "${OUTPUT_DIR}/summary_formatted.json" 2>/dev/null || true
        print_success "Report saved to ${OUTPUT_DIR}"
    fi
}

# Validate results
validate_results() {
    print_header "Validating Results"

    if [ ! -f "${OUTPUT_DIR}/summary.json" ]; then
        print_error "Summary file not found"
        return 1
    fi

    # Parse key metrics from summary
    print_info "Extracting key performance indicators..."

    # This is a simple validation - you can enhance it
    FAILURE_RATE=$(cat "${OUTPUT_DIR}/summary.json" | grep -o '"failed_requests":{"rate":[0-9.]*' | grep -o '[0-9.]*$' || echo "0")

    print_info "Request failure rate: ${FAILURE_RATE}"

    # Check if failure rate is acceptable
    THRESHOLD=0.10  # 10% threshold
    if (( $(echo "$FAILURE_RATE < $THRESHOLD" | bc -l 2>/dev/null || echo "1") )); then
        print_success "Failure rate is within acceptable limits (<${THRESHOLD})"
    else
        print_warning "Failure rate exceeds threshold (>${THRESHOLD})"
    fi
}

# Monitor ClickHouse during test
monitor_clickhouse() {
    print_info "You can monitor ClickHouse during the test with:"
    print_info "  watch -n 1 'curl -s \"http://localhost:8123/?query=SELECT+count(*)+FROM+metrics\"'"
    print_info ""
    print_info "Or use Grafana at http://localhost:3000 (admin/admin)"
    print_info "Or use Kafka UI at http://localhost:8080"
}

# Usage information
usage() {
    echo "Usage: $0 [TEST_PROFILE]"
    echo ""
    echo "Test Profiles:"
    echo "  smoke   - Quick smoke test (30s, 10 VUs, ~1k metrics/sec)"
    echo "  light   - Light load (5min, 100 VUs, ~10k metrics/sec)"
    echo "  medium  - Medium load (15min, 1000 VUs, ~100k metrics/sec) [DEFAULT]"
    echo "  heavy   - Heavy load (30min, 3000 VUs, ~300k metrics/sec)"
    echo "  stress  - Stress test (14min, 4000 VUs, find breaking point)"
    echo ""
    echo "Environment Variables:"
    echo "  BASE_URL    - Collector URL (default: http://localhost:5000)"
    echo "  BATCH_SIZE  - Metrics per batch (default: 100)"
    echo "  DELAY_MS    - Delay between requests in ms (default: 0.1)"
    echo ""
    echo "Examples:"
    echo "  $0 smoke                    # Run smoke test"
    echo "  BASE_URL=http://prod:5000 $0 medium  # Test against production"
    echo "  BATCH_SIZE=500 $0 heavy     # Heavy test with larger batches"
}

# Main execution
main() {
    if [ "$1" = "-h" ] || [ "$1" = "--help" ]; then
        usage
        exit 0
    fi

    print_header "Faro Load Test Runner"

    check_k6
    check_services
    get_baseline_metrics
    monitor_clickhouse

    echo ""
    read -p "Press Enter to start the test, or Ctrl+C to cancel..."
    echo ""

    run_load_test
    TEST_RESULT=$?

    generate_report
    validate_results

    print_header "Test Complete"

    print_info "Results saved to: ${OUTPUT_DIR}"
    print_info "View detailed metrics in:"
    print_info "  - ${OUTPUT_DIR}/summary.json"
    print_info "  - ${OUTPUT_DIR}/results.json"
    print_info "  - ${OUTPUT_DIR}/test.log"

    exit $TEST_RESULT
}

# Run main function
main "$@"
