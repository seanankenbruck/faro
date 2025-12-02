# Faro

A lightweight, self-hosted metrics monitoring and alerting system built with .NET and ClickHouse inspired by a system design problem on [ByteByteGo](https://bytebytego.com/).

## Overview

This solution will demonstrate the ability to build a metrics monitoring and alerting system from scratch using C# and ClickHouse, with data accessible from Grafana via the ClickHouse plugin. As described in the example problem, a metrics monitoring and alerting system contains five components

- Data collection: collect metric data from different sources.
- Data transmission: transfer data from sources to the metrics monitoring system.
- Data storage: organize and store incoming data.
- Alerting: analyze incoming data, detect anomalies, and generate alerts. The system must be able to send alerts to different communication channels.
- Visualization: present data in graphs, charts, etc. Engineers are better at identifying patterns, trends, or problems when data is presented visually, so we need visualization functionality.

## Project Structure
```
Solution Structure:
├── Faro.sln
├── src/
│   ├── Faro.Collector/           # Metrics ingestion service and Kafka producer
│   ├── Faro.Storage/             # ClickHouse abstraction and data access
│   ├── Faro.Consumer/            # Kafka consumer service (writes to ClickHouse)
│   ├── Faro.AlertingEngine/      # Alert rule evaluation and trigger engine
│   ├── Faro.Notifications/       # Email, SMS, PagerDuty integrations
│   ├── Faro.Client/              # SDK for apps to emit metrics
│   └── Faro.Shared/              # Common models, utilities
├── tests/
│   ├── Unit/
│   └── Integration/
└── docker/                       # Docker compose for local dev (Kafka, ClickHouse, Grafana)
```

## System Architecture Overview

Complete end-to-end metrics monitoring and alerting architecture.

```
┌─────────────────┐
│ Metrics Source  │ (Your applications)
│  + Client SDK   │
└────────┬────────┘
         │ HTTP Push
         ▼
┌─────────────────────────┐
│  Metrics Collector      │
│  - Validation           │
│  - Rate limiting        │
│  - Kafka producer       │
└────────┬────────────────┘
         │ Publish to topic (partitioned by metric_name)
         ▼
    ┌────────┐
    │ Kafka  │ (Buffering, reliability, decoupling)
    └────┬───┘
         │ Consume with consumer group
         ▼
┌─────────────────────────┐
│  Consumer Service       │
│  - Batch aggregation    │
│  - Bulk insert          │
└────────┬────────────────┘
         │ Bulk write
         ▼
┌─────────────────────────────────┐
│      ClickHouse DB              │
│  - metrics table (raw data)     │
│  - metrics_1m (aggregated)      │
│  - metrics_1h (aggregated)      │
│  - TTL/retention policies       │
└────────┬────────────────────────┘
         │
         │ Direct SQL queries
         │
    ┌────┴─────────────────────┐
    │                          │
    ▼                          ▼
┌──────────┐          ┌───────────────────┐
│ Grafana  │          │ Alerting Engine   │
│          │          │  - Rule evaluation│
│ Dashboards         │  - Threshold checks
│ Queries  │          │  - Notifications  │
└──────────┘          └────────┬──────────┘
                               │
                               ▼
                      ┌─────────────────┐
                      │  Notifications  │
                      │  - Email        │
                      │  - SMS          │
                      │  - PagerDuty    │
                      │  - Webhooks     │
                      └─────────────────┘
```

### Key Design Decisions

1. **No Query Service**: Grafana and the Alerting Engine query ClickHouse directly using native SQL. ClickHouse's built-in caching and performance eliminates the need for an intermediate query layer.

2. **Kafka for Decoupling**: Provides buffering, reliability, and allows independent scaling of ingestion and storage layers.

3. **Materialized Views**: Pre-aggregated data at 1-minute and 1-hour granularity for fast dashboard queries and alerting.

4. **Partitioning Strategy**: Kafka topics partitioned by `metric_name` to ensure ordered processing and parallelism.

## License

MIT License - see [LICENSE](LICENSE) file for details