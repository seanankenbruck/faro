using System.Net.Http.Json;
using Faro.Shared.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Faro.Client;

public class MetricsClient: IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly MetricsClientOptions _options;
    private readonly ILogger<MetricsClient> _logger;
    private readonly List<MetricPoint> _buffer = new();
    private readonly SemaphoreSlim _bufferLock = new(1, 1);
    private readonly Timer _flushTimer;
    private bool _disposed;

    public MetricsClient(
        HttpClient httpClient,
        IOptions<MetricsClientOptions> options,
        ILogger<MetricsClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        _httpClient.BaseAddress = new Uri(_options.CollectorUrl);

        // Auto-flush timer
        _flushTimer = new Timer(
            async _ => await FlushAsync(),
            null,
            TimeSpan.FromSeconds(_options.FlushIntervalSeconds),
            TimeSpan.FromSeconds(_options.FlushIntervalSeconds));
    }

    /// <summary>
    /// Record a metric value
    /// </summary>
    public async Task RecordAsync(
        string metricName,
        double value,
        Dictionary<string, string>? tags = null)
    {
        var metric = new MetricPoint
        {
            Timestamp = DateTime.UtcNow,
            MetricName = metricName,
            Value = value,
            Tags = tags ?? new Dictionary<string, string>()
        };

        // Automatically add default tags if configured
        if (!string.IsNullOrEmpty(_options.DefaultHost))
        {
            metric.Tags.TryAdd("host", _options.DefaultHost);
        }

        if (!string.IsNullOrEmpty(_options.DefaultService))
        {
            metric.Tags.TryAdd("service", _options.DefaultService);
        }

        if (!string.IsNullOrEmpty(_options.DefaultEnvironment))
        {
            metric.Tags.TryAdd("environment", _options.DefaultEnvironment);
        }

        await _bufferLock.WaitAsync();
        try
        {
            _buffer.Add(metric);
            
            // auto-flush if buffer is full
            if (_buffer.Count >= _options.BufferSize)
            {
                await FlushInternalAsync();
            }
        }
        finally
        {
            _bufferLock.Release();
        }
    }

    /// <summary>
    /// Manually flush buffered metrics
    /// </summary>
    public async Task FlushAsync()
    {
        await _bufferLock.WaitAsync();
        try
        {
            await FlushInternalAsync();
        }
        finally
        {
            _bufferLock.Release();
        }
    }

    private async Task FlushInternalAsync()
    {
        if (_buffer.Count == 0) return;

        var batch = new MetricBatch
        {
            Metrics = new List<MetricPoint>(_buffer),
            BatchTimestamp = DateTime.UtcNow
        };

        _buffer.Clear();

        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/metrics/batch", batch);
            response.EnsureSuccessStatusCode();
            _logger.LogDebug("Flushed {Count} metrics to collector", batch.Metrics.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to flush metrics to collector");
            // On failure, re-add metrics to buffer
            _buffer.AddRange(batch.Metrics);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        _flushTimer.Dispose();
        FlushAsync().GetAwaiter().GetResult();
        _bufferLock.Dispose();

        _disposed = true;
    }

}

public class MetricsClientOptions
{
    public const string SectionName = "MetricsClient";

    public string CollectorUrl { get; set; } = "http://localhost:5000";
    public int BufferSize { get; set; } = 100;
    public int FlushIntervalSeconds { get; set; } = 10;
    public string? DefaultHost { get; set; }
    public string? DefaultService { get; set; }
    public string? DefaultEnvironment { get; set; }
}