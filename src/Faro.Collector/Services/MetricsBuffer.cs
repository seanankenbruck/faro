using System.Threading.Channels;
using Faro.Shared.Models;

namespace Faro.Collector.Services;

/// <summary>
/// Buffers incoming metrics and publishes them to Kafka in batches
/// </summary>
public class MetricsBuffer: BackgroundService
{
    private readonly Channel<MetricPoint> _channel;
    private readonly IKafkaProducerService _kafkaProducer;
    private readonly ILogger<MetricsBuffer> _logger;
    private readonly TimeSpan _flushInterval;
    private readonly int _batchSize;

    public MetricsBuffer(
        IKafkaProducerService kafkaProducer,
        ILogger<MetricsBuffer> logger,
        IConfiguration configuration)
    {
        _kafkaProducer = kafkaProducer;
        _logger = logger;

        // Unbound channel for high throughput
        _channel = Channel.CreateUnbounded<MetricPoint>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        _flushInterval = TimeSpan.FromSeconds(
            configuration.GetValue<int>("MetricsCollector:FlushIntervalSeconds", 10));
        _batchSize = configuration.GetValue<int>("MetricsCollector:BatchSize", 1000);
    }

    /// <summary>
    /// Add a metric to the buffer
    /// </summary>
    public async ValueTask AddMetricAsync(MetricPoint metric)
    {
        await _channel.Writer.WriteAsync(metric);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Metrics buffer started (flush interval: {Interval}s, batch size: {BatchSize})",
            _flushInterval.TotalSeconds,
            _batchSize);

        var batch = new List<MetricPoint>(_batchSize);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                timeoutCts.CancelAfter(_flushInterval);

                try
                {
                    // Collect metrics until batch is full or timeout
                    while (batch.Count < _batchSize && !timeoutCts.Token.IsCancellationRequested)
                    {
                        if (await _channel.Reader.WaitToReadAsync(timeoutCts.Token))
                        {
                            while (batch.Count < _batchSize && _channel.Reader.TryRead(out var metric))
                            {
                                batch.Add(metric);
                            }

                            // If batch is full after reading, exit immediately
                            if (batch.Count >= _batchSize)
                            {
                                break;
                            }
                        }
                    }
                }
                catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !stoppingToken.IsCancellationRequested)
                {
                    // Timeout expired, continue to flush
                }

                // Flush if we have metrics
                if (batch.Count > 0)
                {
                    await FlushBatchAsync(batch.ToList(), stoppingToken);
                    batch.Clear();
                }

            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Metrics buffer stopping...");
        }
        finally
        {
            // Flush remaining metrics
            if (batch.Count > 0)
            {
                await FlushBatchAsync(batch.ToList(), CancellationToken.None);
            }
        }
    }

    private async Task FlushBatchAsync(List<MetricPoint> batch, CancellationToken cancellationToken)
    {
        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            await _kafkaProducer.ProduceBatchAsync(batch, cancellationToken);
            stopwatch.Stop();

            _logger.LogInformation(
                "Published {Count} metrics to Kafka in {ElapsedMs}ms",
                batch.Count,
                stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish {Count} metrics to Kafka", batch.Count);
            // In production implement a dead-letter queue here
        }
    }
}