# Faro End-to-End Integration Testing Guide

This guide provides step-by-step instructions for testing the complete Faro metrics monitoring and alerting system with real data, from metrics ingestion through alert triggering and notification delivery.

## Overview

The integration test will verify:
1. Metrics are sent from a client application to the Collector API
2. Metrics flow through Kafka and are stored in ClickHouse
3. AlertingEngine evaluates alert rules and detects threshold violations
4. Notifications are sent via configured channels (email/webhook)

## Prerequisites

### Infrastructure Running
Ensure all infrastructure services are running:
```bash
cd faro
docker-compose up -d
```

Verify services are healthy:
```bash
docker-compose ps
```

Expected services:
- `kafka` - Port 29092 (internal), 9092 (external)
- `kafka-ui` - Port 8080
- `clickhouse` - Port 9000 (TCP), 8123 (HTTP)
- `grafana` - Port 3000

### Verify ClickHouse Schema
Connect to ClickHouse and verify the schema is initialized:
```bash
docker exec -it faro-clickhouse clickhouse-client \
  --user metrics_user \
  --password metrics_pass_123 \
  --database metrics
```

Run these queries to verify:
```sql
-- Check main metrics table
SHOW CREATE TABLE metrics;

-- Check materialized views exist
SHOW TABLES LIKE 'metrics_%';

-- Should see: metrics_1m, metrics_1h
```

Exit: `exit` or `Ctrl+D`

## Part 1: Start Faro Services

### 1.1 Start the Consumer Service
The Consumer reads metrics from Kafka and writes to ClickHouse.

```bash
cd faro/src/Faro.Consumer
dotnet run
```

Expected output:
```
Metrics consumer service started
Connected to Kafka at localhost:29092
Subscribed to topic: faro-metrics
Consumer group: faro-consumer-group
```

Keep this terminal open. The consumer will log when it processes metrics.

### 1.2 Start the Collector API
The Collector API receives metrics and publishes to Kafka.

Open a new terminal:
```bash
cd faro/src/Faro.Collector
dotnet run
```

Expected output:
```
Now listening on: http://localhost:5000
Application started
Kafka producer initialized: localhost:29092
Metrics buffer started with 1000 batch size
```

Keep this terminal open.

### 1.3 Start the AlertingEngine
The AlertingEngine evaluates alert rules and sends notifications.

Open a new terminal:
```bash
cd faro/src/Faro.AlertingEngine
dotnet run
```

Expected output:
```
AlertingEngine Worker started
Loading alert rules from: ./alert-rules
Loaded 2 alert rules:
  - high-cpu (evaluation interval: 00:01:00)
  - low-memory (evaluation interval: 00:01:00)
Alert evaluation started for 2 rules
```

Keep this terminal open. The engine will log when it evaluates rules.

## Part 2: Configure Notification Channels

Before triggering alerts, configure at least one notification channel.

### Option A: Configure Email Notification Channel

Edit [src/Faro.AlertingEngine/appsettings.json](src/Faro.AlertingEngine/appsettings.json) and add:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  },
  "ConnectionStrings": {
    "ClickHouse": "Host=localhost;Port=9000;Database=metrics;Username=metrics_user;Password=metrics_pass_123"
  },
  "AlertRules": {
    "Directory": "./alert-rules"
  },
  "Notifications": {
    "Channels": {
      "email": {
        "Type": "Email",
        "SmtpHost": "smtp.gmail.com",
        "SmtpPort": 587,
        "UseSsl": true,
        "Username": "your-email@gmail.com",
        "Password": "your-app-password",
        "FromAddress": "your-email@gmail.com",
        "FromName": "Faro Alerting",
        "ToAddresses": ["recipient@example.com"]
      }
    }
  }
}
```

**Gmail Setup:**
1. Go to Google Account settings
2. Enable 2-factor authentication
3. Generate an App Password: https://myaccount.google.com/apppasswords
4. Use the app password in the configuration

### Option B: Configure Webhook Notification Channel

Edit [src/Faro.AlertingEngine/appsettings.json](src/Faro.AlertingEngine/appsettings.json) and add:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  },
  "ConnectionStrings": {
    "ClickHouse": "Host=localhost;Port=9000;Database=metrics;Username=metrics_user;Password=metrics_pass_123"
  },
  "AlertRules": {
    "Directory": "./alert-rules"
  },
  "Notifications": {
    "Channels": {
      "webhook": {
        "Type": "Webhook",
        "Url": "https://webhook.site/your-unique-id",
        "Method": "POST",
        "Headers": {
          "Authorization": "Bearer your-token",
          "Content-Type": "application/json"
        },
        "TimeoutSeconds": 30
      }
    }
  }
}
```

**Webhook Testing Site:**
1. Go to https://webhook.site/
2. Copy your unique URL
3. Use it as the webhook URL
4. View incoming requests in real-time on the website

**Restart AlertingEngine** after configuration changes:
```bash
# Press Ctrl+C in the AlertingEngine terminal, then:
cd faro/src/Faro.AlertingEngine
dotnet run
```

## Part 3: Send Metrics to Trigger an Alert

We'll trigger the "High CPU Usage" alert by sending metrics that exceed 80%.

### 3.1 Create a Test Alert Rule

For faster testing, create a new alert rule with shorter intervals:

Create [alert-rules/test-high-cpu.json](alert-rules/test-high-cpu.json):
```json
{
  "id": "test-high-cpu",
  "name": "Test High CPU Usage",
  "description": "Test alert with short intervals",
  "query": "SELECT avgMerge(avg_value) as value FROM metrics_1m WHERE metric_name = 'cpu.usage' AND minute >= now() - INTERVAL 2 MINUTE",
  "evaluationInterval": "00:00:30",
  "condition": {
    "operator": "GreaterThan",
    "threshold": 80.0
  },
  "for": "00:01:00",
  "notificationChannels": ["email"],
  "labels": {
    "severity": "warning",
    "team": "platform",
    "test": "true"
  },
  "enabled": true
}
```

This rule:
- Evaluates every 30 seconds (`evaluationInterval`)
- Looks at last 2 minutes of data
- Triggers when avg CPU > 80% for 1 minute (`for`)
- Sends email notification

**Restart AlertingEngine** to load the new rule.

### 3.2 Send Metrics via HTTP API

#### Method 1: Using curl (Single Metrics)

Send a single high CPU metric:
```bash
curl -X POST http://localhost:5000/api/metrics/single \
  -H "Content-Type: application/json" \
  -d '{
    "timestamp": "'$(date -u +"%Y-%m-%dT%H:%M:%S.000Z")'",
    "metricName": "cpu.usage",
    "value": 95.5,
    "tags": {
      "host": "test-server-01",
      "core": "cpu0"
    }
  }'
```

Expected response:
```json
{
  "received": 1
}
```

Send multiple metrics to sustain the condition:
```bash
# Send a metric every 10 seconds for 2 minutes
for i in {1..12}; do
  curl -X POST http://localhost:5000/api/metrics/single \
    -H "Content-Type: application/json" \
    -d '{
      "timestamp": "'$(date -u +"%Y-%m-%dT%H:%M:%S.000Z")'",
      "metricName": "cpu.usage",
      "value": '$(echo "85 + $RANDOM % 15" | bc)',
      "tags": {
        "host": "test-server-01",
        "core": "cpu0"
      }
    }'
  echo "Sent metric $i/12"
  sleep 10
done
```

#### Method 2: Using curl (Batch Metrics)

Send a batch of high CPU metrics:
```bash
curl -X POST http://localhost:5000/api/metrics/batch \
  -H "Content-Type: application/json" \
  -d '{
    "metrics": [
      {
        "timestamp": "'$(date -u +"%Y-%m-%dT%H:%M:%S.000Z")'",
        "metricName": "cpu.usage",
        "value": 92.3,
        "tags": {"host": "test-server-01", "core": "cpu0"}
      },
      {
        "timestamp": "'$(date -u +"%Y-%m-%dT%H:%M:%S.000Z")'",
        "metricName": "cpu.usage",
        "value": 88.7,
        "tags": {"host": "test-server-01", "core": "cpu1"}
      },
      {
        "timestamp": "'$(date -u +"%Y-%m-%dT%H:%M:%S.000Z")'",
        "metricName": "cpu.usage",
        "value": 95.1,
        "tags": {"host": "test-server-01", "core": "cpu2"}
      }
    ]
  }'
```

#### Method 3: Using the TestApp

The TestApp is configured to send random metrics. Modify it to send high CPU values:

Edit [src/TestApp/Program.cs](src/TestApp/Program.cs) temporarily:

```csharp
// Change this line (around line 40):
// var cpuValue = random.NextDouble() * 100;

// To this:
var cpuValue = 85 + random.NextDouble() * 15; // Always 85-100%
```

Run the TestApp:
```bash
cd faro/src/TestApp
dotnet run
```

The app will continuously send high CPU metrics. Let it run for 2-3 minutes to trigger the alert.

## Part 4: Verify Metrics Flow

### 4.1 Check Kafka
Open Kafka UI in browser: http://localhost:8080

Navigate to:
- Topics > `faro-metrics`
- Messages tab

You should see messages with your metrics in JSON format.

### 4.2 Check ClickHouse
Query the metrics table to verify data is stored:

```bash
docker exec -it faro-clickhouse clickhouse-client \
  --user metrics_user \
  --password metrics_pass_123 \
  --database metrics \
  --query "SELECT timestamp, metric_name, value, tags FROM metrics WHERE metric_name = 'cpu.usage' ORDER BY timestamp DESC LIMIT 10"
```

Expected output:
```
2025-12-09 18:30:45.123    cpu.usage    95.5    {'host':'test-server-01','core':'cpu0'}
2025-12-09 18:30:35.456    cpu.usage    88.7    {'host':'test-server-01','core':'cpu1'}
...
```

Check the 1-minute aggregations:
```bash
docker exec -it faro-clickhouse clickhouse-client \
  --user metrics_user \
  --password metrics_pass_123 \
  --database metrics \
  --query "SELECT minute, metric_name, avgMerge(avg_value) as avg_value, maxMerge(max_value) as max_value FROM metrics_1m WHERE metric_name = 'cpu.usage' GROUP BY minute, metric_name ORDER BY minute DESC LIMIT 10"
```

### 4.3 Monitor Service Logs

**Collector logs** should show:
```
[INFO] Received 1 metric(s)
[INFO] Flushed 1 metrics to Kafka topic: faro-metrics
```

**Consumer logs** should show:
```
[INFO] Consumed batch of 1 messages from Kafka
[INFO] Inserted 1 metrics into ClickHouse
```

**AlertingEngine logs** should show:
```
[INFO] Evaluating alert rule: test-high-cpu
[INFO] Query result: 92.3
[INFO] Condition met: 92.3 > 80.0
[INFO] Alert state transition: OK -> Pending
```

After the `for` duration (1 minute), you should see:
```
[INFO] Alert state transition: Pending -> Firing
[INFO] Sending notification for alert: test-high-cpu
[INFO] Notification sent via channel: email
```

## Part 5: Verify Alert Firing

### 5.1 Check Alert State

The AlertingEngine keeps alert state in memory. Check the logs for state transitions:

```
[timestamp] [INFO] Alert 'test-high-cpu' state: Pending (condition met for 00:00:30)
[timestamp] [INFO] Alert 'test-high-cpu' state: Pending (condition met for 00:01:00)
[timestamp] [INFO] Alert 'test-high-cpu' state changed: Pending -> Firing
```

### 5.2 Expected Alert Lifecycle

1. **OK** (Initial state)
   - Condition not met
   - No notifications

2. **Pending** (Condition met, waiting)
   - Threshold exceeded
   - Waiting for `for` duration
   - Logs: "Alert state: Pending (condition met for HH:MM:SS)"

3. **Firing** (Alert triggered)
   - Condition met for required duration
   - Notification sent
   - Logs: "Alert state changed: Pending -> Firing"
   - Logs: "Sending notification via channel: email"

4. **Resolved** (Condition cleared)
   - Metrics back below threshold
   - Resolution notification sent
   - Logs: "Alert state changed: Firing -> Resolved"

## Part 6: Verify Notification Delivery

### For Email Notifications

1. Check the recipient inbox
2. Look for email from "Faro Alerting"
3. Subject: "Alert Firing: Test High CPU Usage" (or similar)
4. Body should contain:
   - Alert name and description
   - Current value (e.g., "92.3")
   - Threshold (e.g., "80.0")
   - Timestamp
   - Alert labels

### For Webhook Notifications

1. Open https://webhook.site/ with your unique URL
2. Refresh the page
3. You should see a POST request with JSON payload:

```json
{
  "title": "Alert Firing: Test High CPU Usage",
  "body": "CPU usage is 92.3%, which exceeds the threshold of 80.0%",
  "severity": "Warning",
  "timestamp": "2025-12-09T18:32:15.123Z",
  "metadata": {
    "rule_id": "test-high-cpu",
    "rule_name": "Test High CPU Usage",
    "state": "Firing",
    "value": "92.3",
    "threshold": "80.0",
    "condition": "GreaterThan",
    "severity": "warning",
    "team": "platform"
  }
}
```

## Part 7: Test Alert Resolution

Now send metrics below the threshold to trigger alert resolution.

### 7.1 Send Low CPU Metrics

Stop sending high metrics and send low values:

```bash
# Send low CPU metrics for 2 minutes
for i in {1..12}; do
  curl -X POST http://localhost:5000/api/metrics/single \
    -H "Content-Type: application/json" \
    -d '{
      "timestamp": "'$(date -u +"%Y-%m-%dT%H:%M:%S.000Z")'",
      "metricName": "cpu.usage",
      "value": '$(echo "20 + $RANDOM % 30" | bc)',
      "tags": {
        "host": "test-server-01",
        "core": "cpu0"
      }
    }'
  echo "Sent low CPU metric $i/12"
  sleep 10
done
```

### 7.2 Check Alert Resolution

Watch the AlertingEngine logs:

```
[INFO] Evaluating alert rule: test-high-cpu
[INFO] Query result: 35.2
[INFO] Condition not met: 35.2 <= 80.0
[INFO] Alert state changed: Firing -> Resolved
[INFO] Sending resolution notification via channel: email
```

You should receive a resolution notification:
- Email subject: "Alert Resolved: Test High CPU Usage"
- Webhook title: "Alert Resolved: Test High CPU Usage"

## Part 8: Test Different Alert Types

### 8.1 Test Low Memory Alert

The [alert-rules/low-memory.json](alert-rules/low-memory.json) rule triggers when `memory.available_percent` < 20%.

Send low memory metrics:

```bash
for i in {1..12}; do
  curl -X POST http://localhost:5000/api/metrics/single \
    -H "Content-Type: application/json" \
    -d '{
      "timestamp": "'$(date -u +"%Y-%m-%dT%H:%M:%S.000Z")'",
      "metricName": "memory.available_percent",
      "value": '$(echo "5 + $RANDOM % 10" | bc)',
      "tags": {
        "host": "test-server-01"
      }
    }'
  echo "Sent low memory metric $i/12"
  sleep 10
done
```

Wait for the alert to fire (5 minutes based on the `for` duration).

### 8.2 Test Custom Alert Rule

Create your own alert rule for testing:

Create [alert-rules/test-custom.json](alert-rules/test-custom.json):
```json
{
  "id": "test-custom",
  "name": "Test Custom Metric Alert",
  "description": "Test alert for custom metric",
  "query": "SELECT avgMerge(avg_value) as value FROM metrics_1m WHERE metric_name = 'test.metric' AND minute >= now() - INTERVAL 1 MINUTE",
  "evaluationInterval": "00:00:15",
  "condition": {
    "operator": "GreaterThan",
    "threshold": 100.0
  },
  "for": "00:00:30",
  "notificationChannels": ["webhook"],
  "labels": {
    "severity": "info",
    "test": "true"
  },
  "enabled": true
}
```

Restart AlertingEngine and send test metrics:

```bash
curl -X POST http://localhost:5000/api/metrics/single \
  -H "Content-Type: application/json" \
  -d '{
    "timestamp": "'$(date -u +"%Y-%m-%dT%H:%M:%S.000Z")'",
    "metricName": "test.metric",
    "value": 250.0,
    "tags": {
      "source": "integration-test"
    }
  }'
```

## Part 9: Performance Testing

Test the system under load to verify it can handle high-volume metrics.

### 9.1 Batch Load Test

Send a large batch of metrics:

```bash
# Generate 1000 metrics in a single batch
curl -X POST http://localhost:5000/api/metrics/batch \
  -H "Content-Type: application/json" \
  -d '{
    "metrics": [
      '$(for i in {1..1000}; do
          echo '{
            "timestamp": "'$(date -u +"%Y-%m-%dT%H:%M:%S.000Z")'",
            "metricName": "load.test.metric",
            "value": '$(echo "$RANDOM % 100" | bc)',
            "tags": {"host": "load-test", "batch": "'$i'"}
          }'
          [ $i -lt 1000 ] && echo ","
        done)'
    ]
  }'
```

### 9.2 Sustained Load Test

Use the TestApp or a script to send metrics continuously:

```bash
#!/bin/bash
# save as load-test.sh

for batch in {1..100}; do
  curl -s -X POST http://localhost:5000/api/metrics/batch \
    -H "Content-Type: application/json" \
    -d '{
      "metrics": [
        '$(for i in {1..100}; do
            echo '{
              "timestamp": "'$(date -u +"%Y-%m-%dT%H:%M:%S.000Z")'",
              "metricName": "load.test.metric",
              "value": '$(echo "$RANDOM % 100" | bc)',
              "tags": {"host": "load-test-'$batch'", "iteration": "'$i'"}
            }'
            [ $i -lt 100 ] && echo ","
          done)'
      ]
    }' > /dev/null
  echo "Sent batch $batch/100 (100 metrics)"
  sleep 0.1
done
```

Run the load test:
```bash
chmod +x load-test.sh
./load-test.sh
```

Monitor system resources and service logs during the load test.

## Part 10: Verification Checklist

Use this checklist to confirm end-to-end functionality:

- [ ] **Infrastructure Running**
  - [ ] Kafka is running and accessible
  - [ ] ClickHouse is running and schema is initialized
  - [ ] Kafka UI shows the `faro-metrics` topic

- [ ] **Services Running**
  - [ ] Faro.Collector API is running on port 5000
  - [ ] Faro.Consumer is running and connected to Kafka
  - [ ] Faro.AlertingEngine is running and loaded alert rules

- [ ] **Metrics Ingestion**
  - [ ] Can POST single metric to `/api/metrics/single`
  - [ ] Can POST batch metrics to `/api/metrics/batch`
  - [ ] Metrics appear in Kafka topic (check Kafka UI)
  - [ ] Metrics appear in ClickHouse `metrics` table
  - [ ] Materialized views (`metrics_1m`) are populated

- [ ] **Alert Evaluation**
  - [ ] AlertingEngine logs show rule evaluations
  - [ ] Alert transitions from OK -> Pending when threshold exceeded
  - [ ] Alert transitions from Pending -> Firing after `for` duration
  - [ ] Alert transitions from Firing -> Resolved when condition clears

- [ ] **Notification Delivery**
  - [ ] Email notification received (if configured)
  - [ ] Webhook notification received (if configured)
  - [ ] Notification contains correct alert details
  - [ ] Resolution notification received when alert clears

- [ ] **Data Validation**
  - [ ] Invalid metrics are rejected (bad timestamp, invalid name)
  - [ ] Metrics with missing required fields are rejected
  - [ ] API returns appropriate error responses

## Troubleshooting

### Metrics Not Appearing in ClickHouse

**Check Kafka:**
```bash
# Verify messages in Kafka topic
docker exec -it faro-kafka-1 kafka-console-consumer \
  --bootstrap-server localhost:9092 \
  --topic faro-metrics \
  --from-beginning \
  --max-messages 10
```

**Check Consumer logs:**
- Is it consuming from Kafka?
- Are there any errors inserting into ClickHouse?

**Check ClickHouse connection:**
```bash
docker exec -it faro-clickhouse clickhouse-client \
  --user metrics_user \
  --password metrics_pass_123 \
  --query "SELECT count() FROM metrics.metrics"
```

### Alert Not Firing

**Check alert rule is loaded:**
- Look for "Loaded X alert rules" in AlertingEngine startup logs
- Verify JSON file is valid and in the `alert-rules/` directory

**Check alert evaluation:**
- AlertingEngine should log "Evaluating alert rule: [name]" every evaluation interval
- Check if query returns expected value

**Run alert query manually:**
```bash
docker exec -it faro-clickhouse clickhouse-client \
  --user metrics_user \
  --password metrics_pass_123 \
  --database metrics \
  --query "SELECT avgMerge(avg_value) as value FROM metrics_1m WHERE metric_name = 'cpu.usage' AND minute >= now() - INTERVAL 2 MINUTE"
```

**Check `for` duration:**
- Alert won't fire until condition is met for the specified duration
- Check logs for "Alert state: Pending (condition met for HH:MM:SS)"

### Notification Not Received

**Email:**
- Verify SMTP credentials are correct
- Check for authentication errors in logs
- For Gmail, ensure App Password is used (not regular password)
- Check spam folder

**Webhook:**
- Verify webhook URL is accessible
- Check for HTTP errors in logs (404, 401, 500, etc.)
- Test webhook URL with curl:
  ```bash
  curl -X POST https://webhook.site/your-unique-id \
    -H "Content-Type: application/json" \
    -d '{"test": "message"}'
  ```

### Services Won't Start

**Port conflicts:**
```bash
# Check if ports are already in use
lsof -i :5000  # Collector
lsof -i :9092  # Kafka
lsof -i :9000  # ClickHouse
```

**Kafka not ready:**
- Wait 30-60 seconds after `docker-compose up` for Kafka to be ready
- Check Kafka logs: `docker logs faro-kafka-1`

## Cleanup

After testing, you can clean up:

### Stop Services
```bash
# Stop Faro services (Ctrl+C in each terminal)
# Or if running in background:
pkill -f "dotnet.*Faro"
```

### Clear Test Data
```bash
# Clear ClickHouse data
docker exec -it faro-clickhouse clickhouse-client \
  --user metrics_user \
  --password metrics_pass_123 \
  --database metrics \
  --query "TRUNCATE TABLE metrics"

# Or reset everything:
docker-compose down -v  # Warning: removes all data
```

### Remove Test Alert Rules
```bash
rm alert-rules/test-*.json
```

## Appendix: Quick Commands Reference

### Send Single Metric
```bash
curl -X POST http://localhost:5000/api/metrics/single \
  -H "Content-Type: application/json" \
  -d '{"timestamp":"'$(date -u +"%Y-%m-%dT%H:%M:%S.000Z")'","metricName":"test.metric","value":42,"tags":{"host":"test"}}'
```

### Query Metrics
```bash
docker exec -it faro-clickhouse clickhouse-client \
  --user metrics_user --password metrics_pass_123 --database metrics \
  --query "SELECT * FROM metrics ORDER BY timestamp DESC LIMIT 10"
```

### Check Kafka Messages
```bash
docker exec -it faro-kafka-1 kafka-console-consumer \
  --bootstrap-server localhost:9092 --topic faro-metrics --from-beginning --max-messages 5
```

### Restart AlertingEngine
```bash
cd src/Faro.AlertingEngine && dotnet run
```

### Test Webhook
```bash
curl -X POST https://webhook.site/your-id -H "Content-Type: application/json" -d '{"test":"message"}'
```
