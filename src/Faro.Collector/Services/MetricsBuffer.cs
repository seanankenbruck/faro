using System.Threading.Channels;
using Faro.Shared.Models;
using Faro.Storage;

namespace Faro.Collector.Services;

/// <summary>
/// Buffers incoming metrics and flushes them in batches
/// </summary>
public class MetricsBuffer: BackgroundService
{
    private readonly Channel<MetricPoint> _channel;
    private readonly IMetricsRepository _repository;
    private readonly ILogger<MetricsBuffer> _logger;
    private readonly TimeSpan _flushInterval;
    private readonly int _batchSize;

    public MetricsBuffer(
        IMetricsRepository repository,
        ILogger<MetricsBuffer> logger,
        IConfiguration configuration)
    {
        _repository = repository;
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
                    await FlushBatchAsync(batch, stoppingToken);
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
                await FlushBatchAsync(batch, CancellationToken.None);
            }
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
                "Flushed {Count} metrics to storage in {ElapsedMs}ms",
                batch.Count,
                stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to flush {Count} metrics", batch.Count);
            // In production implement a dead-letter queue here
        }
    }
}