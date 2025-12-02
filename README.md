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
│   ├── Faro.Collector/           # Metrics ingestion service and kafka producer
│   ├── Faro.Storage/             # Time series DB abstraction
│   ├── Faro.Consumer/            # Kafka consumer service
│   ├── Faro.QueryService/        # Query API
│   ├── Faro.AlertingEngine/      # Alert evaluation
│   ├── Faro.Notifications/       # Email, SMS, PagerDuty
│   ├── Faro.Client/              # SDK for apps to emit metrics
│   └── Faro.Shared/              # Common models, utilities
├── tests/
│   ├── Unit/
│   └── Integration/
└── docker/                                     # Docker compose for local dev
```

## System Architecture Overview

Initial system architecture breakdown. 

```
┌─────────────────┐
│ Metrics Source  │ (Your applications)
│  + Client SDK   │
└────────┬────────┘
         │ HTTP/gRPC Push
         ▼
┌─────────────────────────┐
│  Metrics Collector      │
│  - Validation           │
│  - Batching (10-30s)    │
│  - Buffer management    │
└────────┬────────────────┘
         │ Bulk Insert
         ▼
┌─────────────────────────┐
│    ClickHouse DB        │
│  - metrics table        │
│  - materialized views   │
│  - retention policies   │
└─────────────────────────┘
```

## License

MIT License - see [LICENSE](LICENSE) file for details