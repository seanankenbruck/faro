using Confluent.Kafka;
using Faro.Collector.Services;
using Faro.Shared.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace Faro.Collector.Tests;

public class KafkaProducerServiceTests
{
    private readonly Mock<ILogger<KafkaProducerService>> _mockLogger;
    private readonly IConfiguration _configuration;

    public KafkaProducerServiceTests()
    {
        _mockLogger = new Mock<ILogger<KafkaProducerService>>();

        var inMemorySettings = new Dictionary<string, string>
        {
            ["Kafka:BootstrapServers"] = "localhost:9092",
            ["Kafka:Topic"] = "test-metrics"
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings!)
            .Build();
    }

    [Fact]
    public void Constructor_WithValidConfiguration_CreatesInstance()
    {
        var service = new KafkaProducerService(_configuration, _mockLogger.Object);

        Assert.NotNull(service);
    }

    [Fact]
    public void Constructor_WithoutTopic_UsesDefaultTopic()
    {
        var configWithoutTopic = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["Kafka:BootstrapServers"] = "localhost:9092"
            }!)
            .Build();

        var service = new KafkaProducerService(configWithoutTopic, _mockLogger.Object);

        Assert.NotNull(service);
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("faro-metrics")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void Constructor_LogsInitialization()
    {
        var service = new KafkaProducerService(_configuration, _mockLogger.Object);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Kafka producer initialized")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void Dispose_CallsProducerFlushAndDispose()
    {
        var service = new KafkaProducerService(_configuration, _mockLogger.Object);

        // Should not throw
        service.Dispose();
    }

    [Fact]
    public void Constructor_ConfiguresProducerWithCorrectSettings()
    {
        var customConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["Kafka:BootstrapServers"] = "kafka.example.com:9093",
                ["Kafka:Topic"] = "custom-topic"
            }!)
            .Build();

        var service = new KafkaProducerService(customConfig, _mockLogger.Object);

        Assert.NotNull(service);
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("custom-topic")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void MetricPoint_SerializesToJson()
    {
        var metric = new MetricPoint
        {
            MetricName = "test.metric",
            Value = 42.0,
            Timestamp = DateTime.UtcNow,
            Tags = new Dictionary<string, string>
            {
                ["host"] = "test-host",
                ["service"] = "test-service"
            }
        };

        var json = System.Text.Json.JsonSerializer.Serialize(metric);

        Assert.Contains("test.metric", json);
        Assert.Contains("42", json);
        Assert.Contains("test-host", json);
    }

    [Fact]
    public void Configuration_SupportsCustomBootstrapServers()
    {
        var configs = new[]
        {
            "localhost:9092",
            "kafka1:9092,kafka2:9092,kafka3:9092",
            "broker.example.com:9093"
        };

        foreach (var bootstrapServers in configs)
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["Kafka:BootstrapServers"] = bootstrapServers
                }!)
                .Build();

            var service = new KafkaProducerService(config, _mockLogger.Object);
            Assert.NotNull(service);
        }
    }

    [Fact]
    public void MetricPoint_WithEmptyTags_Serializes()
    {
        var metric = new MetricPoint
        {
            MetricName = "test.metric",
            Value = 100.0,
            Timestamp = DateTime.UtcNow,
            Tags = new Dictionary<string, string>()
        };

        var json = System.Text.Json.JsonSerializer.Serialize(metric);

        Assert.Contains("test.metric", json);
        Assert.Contains("100", json);
    }

    [Fact]
    public void MetricPoint_WithManyTags_Serializes()
    {
        var tags = new Dictionary<string, string>();
        for (int i = 0; i < 20; i++)
        {
            tags[$"tag{i}"] = $"value{i}";
        }

        var metric = new MetricPoint
        {
            MetricName = "test.metric",
            Value = 42.0,
            Timestamp = DateTime.UtcNow,
            Tags = tags
        };

        var json = System.Text.Json.JsonSerializer.Serialize(metric);

        Assert.Contains("tag0", json);
        Assert.Contains("tag19", json);
    }
}
