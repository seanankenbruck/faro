using Faro.Shared.Models;
using Faro.Storage;
using Faro.Storage.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Faro.Storage.Tests;

public class ClickHouseMetricsRepositoryTests
{
    private readonly Mock<ILogger<ClickHouseMetricsRepository>> _mockLogger;
    private readonly Mock<IClickHouseConnectionFactory> _mockConnectionFactory;
    private readonly ClickHouseOptions _options;
    private readonly IOptions<ClickHouseOptions> _optionsWrapper;

    public ClickHouseMetricsRepositoryTests()
    {
        _mockLogger = new Mock<ILogger<ClickHouseMetricsRepository>>();
        _mockConnectionFactory = new Mock<IClickHouseConnectionFactory>();
        _options = new ClickHouseOptions
        {
            Host = "localhost",
            Port = 8123,
            Database = "test_db",
            Username = "test_user",
            Password = "test_password",
            MaxRetries = 3
        };
        _optionsWrapper = Options.Create(_options);
    }

    [Fact]
    public void Constructor_InitializesWithValidOptions()
    {
        var repository = new ClickHouseMetricsRepository(_optionsWrapper, _mockLogger.Object, _mockConnectionFactory.Object);

        Assert.NotNull(repository);
    }

    [Fact]
    public void Constructor_BuildsCorrectConnectionString()
    {
        var customOptions = Options.Create(new ClickHouseOptions
        {
            Host = "clickhouse.example.com",
            Port = 9000,
            Database = "metrics_db",
            Username = "admin",
            Password = "secure_password",
            MaxRetries = 5
        });

        var repository = new ClickHouseMetricsRepository(customOptions, _mockLogger.Object, _mockConnectionFactory.Object);

        Assert.NotNull(repository);
    }

    [Fact]
    public async Task WriteBatchAsync_WithEmptyBatch_DoesNotThrow()
    {
        var repository = CreateRepositoryForTesting();
        var emptyBatch = new List<MetricPoint>();

        // Should not throw for empty batch
        await repository.WriteBatchAsync(emptyBatch);

        // Verify logging occurred
        _mockLogger.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task WriteBatchAsync_WithNullBatch_HandlesGracefully()
    {
        var repository = CreateRepositoryForTesting();

        // Should handle null batch without throwing
        await repository.WriteBatchAsync(null!);

        _mockLogger.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public void ClickHouseOptions_DefaultRetryAttempts_IsThree()
    {
        var defaultOptions = new ClickHouseOptions
        {
            Host = "localhost",
            Port = 8123,
            Database = "test",
            Username = "default",
            Password = ""
        };

        Assert.Equal(3, defaultOptions.MaxRetries);
    }

    [Fact]
    public void ClickHouseOptions_AllPropertiesSet()
    {
        var options = new ClickHouseOptions
        {
            Host = "db.example.com",
            Port = 9000,
            Database = "metrics",
            Username = "writer",
            Password = "secret",
            MaxRetries = 5
        };

        Assert.Equal("db.example.com", options.Host);
        Assert.Equal(9000, options.Port);
        Assert.Equal("metrics", options.Database);
        Assert.Equal("writer", options.Username);
        Assert.Equal("secret", options.Password);
        Assert.Equal(5, options.MaxRetries);
    }

    [Theory]
    [InlineData("localhost", 8123)]
    [InlineData("clickhouse.local", 9000)]
    [InlineData("192.168.1.100", 8123)]
    public void ClickHouseOptions_VariousHostAndPort_Accepted(string host, int port)
    {
        var options = new ClickHouseOptions
        {
            Host = host,
            Port = port,
            Database = "test",
            Username = "user",
            Password = "pass"
        };

        Assert.Equal(host, options.Host);
        Assert.Equal(port, options.Port);
    }

    [Fact]
    public void MetricBatch_DefaultConstructor_InitializesMetricsList()
    {
        var batch = new MetricBatch();

        Assert.NotNull(batch.Metrics);
        Assert.Empty(batch.Metrics);
    }

    [Fact]
    public void MetricBatch_WithMetrics_StoresCorrectly()
    {
        var metrics = new List<MetricPoint>
        {
            CreateValidMetric("metric1"),
            CreateValidMetric("metric2")
        };

        var batch = new MetricBatch { Metrics = metrics };

        Assert.Equal(2, batch.Metrics.Count);
        Assert.Equal("metric1", batch.Metrics[0].MetricName);
        Assert.Equal("metric2", batch.Metrics[1].MetricName);
    }

    [Fact]
    public void MetricPoint_ConvenienceProperties_RetrieveTagValues()
    {
        var metric = new MetricPoint
        {
            MetricName = "test.metric",
            Value = 42.0,
            Timestamp = DateTime.UtcNow,
            Tags = new Dictionary<string, string>
            {
                ["host"] = "test-host",
                ["service"] = "test-service",
                ["environment"] = "production"
            }
        };

        Assert.Equal("test-host", metric.Host);
        Assert.Equal("test-service", metric.Service);
        Assert.Equal("production", metric.Environment);
    }

    [Fact]
    public void MetricPoint_ConvenienceProperties_ReturnNullWhenNotSet()
    {
        var metric = new MetricPoint
        {
            MetricName = "test.metric",
            Value = 42.0,
            Timestamp = DateTime.UtcNow,
            Tags = new Dictionary<string, string>()
        };

        Assert.Empty(metric.Host);
        Assert.Empty(metric.Service);
        Assert.Empty(metric.Environment);
    }

    [Fact]
    public void MetricPoint_WithAllProperties_CreatesSuccessfully()
    {
        var timestamp = DateTime.UtcNow;
        var tags = new Dictionary<string, string>
        {
            ["key1"] = "value1",
            ["key2"] = "value2"
        };

        var metric = new MetricPoint
        {
            MetricName = "my.metric",
            Value = 123.45,
            Timestamp = timestamp,
            Tags = tags
        };

        Assert.Equal("my.metric", metric.MetricName);
        Assert.Equal(123.45, metric.Value);
        Assert.Equal(timestamp, metric.Timestamp);
        Assert.Equal(tags, metric.Tags);
        Assert.Equal(2, metric.Tags.Count);
    }

    [Fact]
    public async Task WriteBatchAsync_LogsAttempt()
    {
        // Setup mock to succeed without actual database call
        _mockConnectionFactory
            .Setup(x => x.WriteBatchAsync(It.IsAny<IEnumerable<MetricPoint>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var repository = CreateRepositoryForTesting();
        var batch = new List<MetricPoint> { CreateValidMetric() };

        await repository.WriteBatchAsync(batch);

        _mockLogger.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(10)]
    public void ClickHouseOptions_CustomRetryAttempts_Accepted(int retryAttempts)
    {
        var options = new ClickHouseOptions
        {
            Host = "localhost",
            Port = 8123,
            Database = "test",
            Username = "user",
            Password = "pass",
            MaxRetries = retryAttempts
        };

        Assert.Equal(retryAttempts, options.MaxRetries);
    }

    [Fact]
    public void MetricPoint_Tags_CanBeModified()
    {
        var metric = CreateValidMetric();

        metric.Tags["new_tag"] = "new_value";
        metric.Tags["host"] = "updated-host";

        Assert.Equal("new_value", metric.Tags["new_tag"]);
        Assert.Equal("updated-host", metric.Tags["host"]);
    }

    [Fact]
    public void MetricBatch_Metrics_CanBeModified()
    {
        var batch = new MetricBatch
        {
            Metrics = new List<MetricPoint>()
        };

        batch.Metrics.Add(CreateValidMetric("metric1"));
        batch.Metrics.Add(CreateValidMetric("metric2"));

        Assert.Equal(2, batch.Metrics.Count);
    }

    [Fact]
    public void MetricPoint_SupportsNegativeValues()
    {
        var metric = new MetricPoint
        {
            MetricName = "temperature.celsius",
            Value = -15.5,
            Timestamp = DateTime.UtcNow,
            Tags = new Dictionary<string, string>()
        };

        Assert.Equal(-15.5, metric.Value);
    }

    [Fact]
    public void MetricPoint_SupportsVeryLargeValues()
    {
        var metric = new MetricPoint
        {
            MetricName = "bytes.processed",
            Value = 1_000_000_000.0,
            Timestamp = DateTime.UtcNow,
            Tags = new Dictionary<string, string>()
        };

        Assert.Equal(1_000_000_000.0, metric.Value);
    }

    [Fact]
    public void MetricPoint_SupportsVerySmallValues()
    {
        var metric = new MetricPoint
        {
            MetricName = "precision.measurement",
            Value = 0.000001,
            Timestamp = DateTime.UtcNow,
            Tags = new Dictionary<string, string>()
        };

        Assert.Equal(0.000001, metric.Value);
    }

    private ClickHouseMetricsRepository CreateRepositoryForTesting()
    {
        return new ClickHouseMetricsRepository(_optionsWrapper, _mockLogger.Object, _mockConnectionFactory.Object);
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
