using Faro.Collector.Services;
using Faro.Shared.Models;
using Faro.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace Faro.Collector.Tests;

public class MetricsBufferTests
{
    private readonly Mock<IKafkaProducerService> _kafkaProducer;
    private readonly Mock<ILogger<MetricsBuffer>> _mockLogger;
    private readonly IConfiguration _configuration;

    public MetricsBufferTests()
    {
        _kafkaProducer = new Mock<IKafkaProducerService>();
        _mockLogger = new Mock<ILogger<MetricsBuffer>>();

        var inMemorySettings = new Dictionary<string, string>
        {
            ["MetricsCollector:BatchSize"] = "5",
            ["MetricsCollector:FlushIntervalSeconds"] = "1"
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings!)
            .Build();
    }

    [Fact]
    public async Task AddMetricAsync_AcceptsMetric()
    {
        var buffer = new MetricsBuffer(_kafkaProducer.Object, _mockLogger.Object, _configuration);
        var metric = CreateValidMetric();

        await buffer.AddMetricAsync(metric);

        // No assertion needed - just verifying no exceptions
        await buffer.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task FlushesWhenBatchSizeReached()
    {
        _kafkaProducer.Setup(k => k.ProduceBatchAsync(It.IsAny<IEnumerable<MetricPoint>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var buffer = new MetricsBuffer(_kafkaProducer.Object, _mockLogger.Object, _configuration);
        await buffer.StartAsync(CancellationToken.None);

        // Give the background task time to start waiting for metrics
        await Task.Delay(50);

        // Add metrics equal to batch size
        for (int i = 0; i < 5; i++)
        {
            await buffer.AddMetricAsync(CreateValidMetric($"metric{i}"));
        }

        // Give time for async flush (increased to avoid race condition)
        await Task.Delay(200);

        _kafkaProducer.Verify(
            k => k.ProduceBatchAsync(It.Is<IEnumerable<MetricPoint>>(m => m.Count() == 5), It.IsAny<CancellationToken>()),
            Times.Once);

        await buffer.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task FlushesOnInterval()
    {
        _kafkaProducer.Setup(r => r.ProduceBatchAsync(It.IsAny<IEnumerable<MetricPoint>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var buffer = new MetricsBuffer(_kafkaProducer.Object, _mockLogger.Object, _configuration);
        await buffer.StartAsync(CancellationToken.None);

        // Add fewer metrics than batch size
        await buffer.AddMetricAsync(CreateValidMetric("metric1"));
        await buffer.AddMetricAsync(CreateValidMetric("metric2"));

        // Wait for flush interval to trigger
        await Task.Delay(1500);

        _kafkaProducer.Verify(
            r => r.ProduceBatchAsync(It.Is<IEnumerable<MetricPoint>>(m => m.Count() == 2), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);

        await buffer.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StopAsync_FlushesRemainingMetrics()
    {
        _kafkaProducer.Setup(r => r.ProduceBatchAsync(It.IsAny<IEnumerable<MetricPoint>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var buffer = new MetricsBuffer(_kafkaProducer.Object, _mockLogger.Object, _configuration);
        await buffer.StartAsync(CancellationToken.None);

        // Add some metrics
        await buffer.AddMetricAsync(CreateValidMetric("metric1"));
        await buffer.AddMetricAsync(CreateValidMetric("metric2"));
        await buffer.AddMetricAsync(CreateValidMetric("metric3"));

        // Stop should flush remaining metrics
        await buffer.StopAsync(CancellationToken.None);

        _kafkaProducer.Verify(
            r => r.ProduceBatchAsync(It.Is<IEnumerable<MetricPoint>>(m => m.Count() == 3), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task MultipleBatches_ProcessedSequentially()
    {
        _kafkaProducer.Setup(r => r.ProduceBatchAsync(It.IsAny<IEnumerable<MetricPoint>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var buffer = new MetricsBuffer(_kafkaProducer.Object, _mockLogger.Object, _configuration);
        await buffer.StartAsync(CancellationToken.None);

        // Add two batches worth of metrics
        for (int i = 0; i < 12; i++)
        {
            await buffer.AddMetricAsync(CreateValidMetric($"metric{i}"));
        }

        // Give time for flushes
        await Task.Delay(1000);

        // Should have flushed at least twice (5 + 5 + 2)
        _kafkaProducer.Verify(
            r => r.ProduceBatchAsync(It.IsAny<IEnumerable<MetricPoint>>(), It.IsAny<CancellationToken>()),
            Times.AtLeast(2));

        await buffer.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ConcurrentMetrics_HandledSafely()
    {
        _kafkaProducer.Setup(r => r.ProduceBatchAsync(It.IsAny<IEnumerable<MetricPoint>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var buffer = new MetricsBuffer(_kafkaProducer.Object, _mockLogger.Object, _configuration);
        await buffer.StartAsync(CancellationToken.None);

        // Add metrics concurrently
        var tasks = Enumerable.Range(0, 20)
            .Select(i => buffer.AddMetricAsync(CreateValidMetric($"metric{i}")).AsTask())
            .ToArray();

        await Task.WhenAll(tasks);
        await Task.Delay(500);

        await buffer.StopAsync(CancellationToken.None);

        // Verify all metrics were processed
        var totalMetricsWritten = _kafkaProducer.Invocations
            .Where(inv => inv.Method.Name == nameof(IKafkaProducerService.ProduceBatchAsync))
            .Sum(inv => ((IEnumerable<MetricPoint>)inv.Arguments[0]).Count());

        Assert.Equal(20, totalMetricsWritten);
    }

    [Fact]
    public async Task RepositoryException_LogsError()
    {
        _kafkaProducer.Setup(r => r.ProduceBatchAsync(It.IsAny<IEnumerable<MetricPoint>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database connection failed"));

        var buffer = new MetricsBuffer(_kafkaProducer.Object, _mockLogger.Object, _configuration);
        await buffer.StartAsync(CancellationToken.None);

        // Give the background task time to start waiting for metrics
        await Task.Delay(50);

        await buffer.AddMetricAsync(CreateValidMetric());

        // Wait for flush interval to trigger (1 second + buffer)
        await Task.Delay(1200);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);

        await buffer.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task EmptyBatch_NotSentToRepository()
    {
        _kafkaProducer.Setup(r => r.ProduceBatchAsync(It.IsAny<IEnumerable<MetricPoint>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var buffer = new MetricsBuffer(_kafkaProducer.Object, _mockLogger.Object, _configuration);
        await buffer.StartAsync(CancellationToken.None);

        // Don't add any metrics, just wait for interval
        await Task.Delay(1500);

        _kafkaProducer.Verify(
            r => r.ProduceBatchAsync(It.IsAny<IEnumerable<MetricPoint>>(), It.IsAny<CancellationToken>()),
            Times.Never);

        await buffer.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task LogsFlushPerformanceMetrics()
    {
        _kafkaProducer.Setup(r => r.ProduceBatchAsync(It.IsAny<IEnumerable<MetricPoint>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var buffer = new MetricsBuffer(_kafkaProducer.Object, _mockLogger.Object, _configuration);
        await buffer.StartAsync(CancellationToken.None);

        for (int i = 0; i < 5; i++)
        {
            await buffer.AddMetricAsync(CreateValidMetric($"metric{i}"));
        }

        await Task.Delay(500);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Published")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);

        await buffer.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task CustomBatchSize_RespectedInConfiguration()
    {
        var customSettings = new Dictionary<string, string>
        {
            ["MetricsCollector:BatchSize"] = "10",
            ["MetricsCollector:FlushIntervalSeconds"] = "60"
        };
        var customConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(customSettings!)
            .Build();

        _kafkaProducer.Setup(r => r.ProduceBatchAsync(It.IsAny<IEnumerable<MetricPoint>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var buffer = new MetricsBuffer(_kafkaProducer.Object, _mockLogger.Object, customConfig);
        await buffer.StartAsync(CancellationToken.None);

        // Add 10 metrics to trigger batch
        for (int i = 0; i < 10; i++)
        {
            await buffer.AddMetricAsync(CreateValidMetric($"metric{i}"));
        }

        await Task.Delay(500);

        _kafkaProducer.Verify(
            r => r.ProduceBatchAsync(It.Is<IEnumerable<MetricPoint>>(m => m.Count() == 10), It.IsAny<CancellationToken>()),
            Times.Once);

        await buffer.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task CancellationToken_StopsProcessing()
    {
        _kafkaProducer.Setup(r => r.ProduceBatchAsync(It.IsAny<IEnumerable<MetricPoint>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var buffer = new MetricsBuffer(_kafkaProducer.Object, _mockLogger.Object, _configuration);
        var cts = new CancellationTokenSource();

        await buffer.StartAsync(cts.Token);
        await buffer.AddMetricAsync(CreateValidMetric());

        cts.Cancel();
        await buffer.StopAsync(CancellationToken.None);

        // Should have flushed remaining metrics before stopping
        _kafkaProducer.Verify(
            r => r.ProduceBatchAsync(It.IsAny<IEnumerable<MetricPoint>>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    private MetricPoint CreateValidMetric(string name = "test.metric")
    {
        return new MetricPoint
        {
            MetricName = name,
            Value = 42.0,
            Timestamp = DateTime.UtcNow,
            Tags = new Dictionary<string, string>
            {
                ["host"] = "test-host",
                ["service"] = "test-service"
            }
        };
    }
}
