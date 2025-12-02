#!/bin/bash

# Wait for kafka to be ready
echo "Waiting for Kafka to be ready..."
until /usr/bin/kafka-topics --bootstrap-server kafka:9092 --list &> /dev/null; do
  echo "Waiting for Kafka to be ready..."
  sleep 5
done

# Create the topic with 12 paritions and a replication factor of 1
echo "Kafka is ready. Creating topic..."
/usr/bin/kafka-topics --create \
--bootstrap-server kafka:9092 \
--topic faro-metrics \
--partitions 12 \
--replication-factor 1 \
--config retention.ms=86400000 \
--config segment.bytes=1073741824 \
--config compression.type=snappy \
--if-not-exists

# Verify the topic was created
echo "Topic created successfully. Details:"
kafka-topics --describe \
--bootstrap-server kafka:9092 \
--topic faro-metrics