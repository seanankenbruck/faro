using Confluent.Kafka;
using System.Text.Json;
using Faro.Shared.Models;

namespace Faro.Collector.Services;

public interface IKafkaProducerService
{
    Task ProduceAsync(MetricPoint metric, CancellationToken cancellationToken = default);
    Task ProduceBatchAsync(IEnumerable<MetricPoint> metrics, CancellationToken cancellationToken = default);
}

public class KafkaProducerService : IKafkaProducerService, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly string _topic;
    private readonly ILogger<KafkaProducerService> _logger;

    public KafkaProducerService(IConfiguration configuration, ILogger<KafkaProducerService> logger)
    {
        _logger = logger;
        _topic = configuration["Kafka:Topic"] ?? "faro-metrics";

        var config = new ProducerConfig
        {
            BootstrapServers = configuration["Kafka:BootstrapServers"],
            Acks = Acks.All,
            EnableIdempotence = true,
            MaxInFlight = 5,
            CompressionType = CompressionType.Snappy,
            LingerMs = 100,
            BatchSize = 16384
        };

        _producer = new ProducerBuilder<string, string>(config)
            .SetErrorHandler((_, e) => _logger.LogError("Kafka error: {Reason}", e.Reason))
            .Build();

        _logger.LogInformation("Kafka producer initialized for topic: {Topic}", _topic);
    }

    public async Task ProduceAsync(MetricPoint metric, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(metric);
        var message = new Message<string, string>
        {
            Key = metric.MetricName,
            Value = json,
            Timestamp = new Timestamp(metric.Timestamp)
        };

        await _producer.ProduceAsync(_topic, message, cancellationToken);
    }

    public async Task ProduceBatchAsync(IEnumerable<MetricPoint> metrics, CancellationToken cancellationToken = default)
    {
        var tasks = metrics.Select(m => ProduceAsync(m, cancellationToken));
        await Task.WhenAll(tasks);
        _producer.Flush(cancellationToken);
    }

    public void Dispose()
    {
        _producer?.Flush(TimeSpan.FromSeconds(10));
        _producer?.Dispose();
    }
}
