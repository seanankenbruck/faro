using Confluent.Kafka;
using System.Text.Json;
using Faro.Shared.Models;
using Faro.Storage;

namespace Faro.Consumer.Services;

public class MetricsConsumerService : BackgroundService
{
    private readonly IConsumer<string, string> _consumer;
    private readonly IMetricsRepository _repository;
    private readonly ILogger<MetricsConsumerService> _logger;
    private readonly int _batchSize;
    private readonly TimeSpan _batchTimeout;
    private readonly string _topic;

    public MetricsConsumerService(
        IConfiguration configuration,
        IMetricsRepository repository,
        ILogger<MetricsConsumerService> logger)
    {
        _repository = repository;
        _logger = logger;
        _topic = configuration["Kafka:Topic"] ?? "faro-metrics";
        _batchSize = configuration.GetValue<int>("Consumer:BatchSize", 1000);
        _batchTimeout = TimeSpan.FromSeconds(
            configuration.GetValue<int>("Consumer:BatchTimeoutSeconds", 10));

        var config = new ConsumerConfig
        {
            BootstrapServers = configuration["Kafka:BootstrapServers"],
            GroupId = configuration["Kafka:GroupId"],
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            MaxPollIntervalMs = 300000,
            SessionTimeoutMs = 45000,
            PartitionAssignmentStrategy = PartitionAssignmentStrategy.CooperativeSticky
        };

        _consumer = new ConsumerBuilder<string, string>(config)
            .SetErrorHandler((_, e) => _logger.LogError("Kafka consumer error: {Reason}", e.Reason))
            .SetPartitionsAssignedHandler((consumer, partitions) =>
            {
                // log when partitions are assigned
                _logger.LogInformation("Assigned {Count} partitions: [{Partitions}]",
                    partitions.Count,
                    string.Join(", ", partitions.Select(p => p.Partition.Value)));
            })
            .SetPartitionsRevokedHandler((consumer, partitions) =>
            {
                // log when partitions are revoked (rebalancing)
                _logger.LogInformation("Revoked {Count} partitions: [{Partitions}]",
                    partitions.Count,
                    string.Join(", ", partitions.Select(p => p.Partition.Value)));
            })
            .Build();

        _logger.LogInformation(
            "Kafka consumer initialized (topic: {Topic}, group: {GroupId})",
            _topic, config.GroupId);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _consumer.Subscribe(_topic);
        _logger.LogInformation("Subscribed to topic: {Topic}", _topic);

        var batch = new List<MetricPoint>();
        var lastFlushTime = DateTime.UtcNow;

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var result = _consumer.Consume(TimeSpan.FromSeconds(1));

                    if (result != null)
                    {
                        var metric = JsonSerializer.Deserialize<MetricPoint>(result.Message.Value);
                        if (metric != null)
                        {
                            batch.Add(metric);
                        }

                        // Flush if batch is full or timeout reached
                        var shouldFlush = batch.Count >= _batchSize ||
                                        (DateTime.UtcNow - lastFlushTime) >= _batchTimeout;

                        if (shouldFlush && batch.Count > 0)
                        {
                            await FlushBatchAsync(batch, stoppingToken);
                            _consumer.Commit(result);
                            batch.Clear();
                            lastFlushTime = DateTime.UtcNow;
                        }
                    }
                    else if (batch.Count > 0 && (DateTime.UtcNow - lastFlushTime) >= _batchTimeout)
                    {
                        // Timeout flush
                        await FlushBatchAsync(batch, stoppingToken);
                        _consumer.Commit();
                        batch.Clear();
                        lastFlushTime = DateTime.UtcNow;
                    }
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(ex, "Error consuming message from Kafka");
                }
            }
        }
        finally
        {
            // Flush remaining metrics
            if (batch.Count > 0)
            {
                await FlushBatchAsync(batch, CancellationToken.None);
            }

            _consumer.Close();
            _consumer.Dispose();
        }
    }

    private async Task FlushBatchAsync(List<MetricPoint> batch, CancellationToken cancellationToken)
    {
        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            await _repository.WriteBatchAsync(batch, cancellationToken);
            stopwatch.Stop();

            _logger.LogInformation(
                "Flushed {Count} metrics to ClickHouse in {ElapsedMs}ms",
                batch.Count,
                stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to flush {Count} metrics to ClickHouse", batch.Count);
            throw; // Let Kafka retry
        }
    }
}
