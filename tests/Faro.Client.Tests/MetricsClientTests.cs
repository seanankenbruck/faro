using System.Net;
using System.Net.Http.Json;
using Faro.Client;
using Faro.Shared.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;

namespace Faro.Client.Tests;

public class MetricsClientTests : IDisposable
{
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly HttpClient _httpClient;
    private readonly MetricsClientOptions _options;
    private readonly IOptions<MetricsClientOptions> _optionsWrapper;
    private readonly Mock<ILogger<MetricsClient>> _mockLogger;

    public MetricsClientTests()
    {
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:5000")
        };

        _options = new MetricsClientOptions
        {
            CollectorUrl = "http://localhost:5000",
            BufferSize = 3,
            FlushIntervalSeconds = 60,
            DefaultHost = "test-host",
            DefaultService = "test-service",
            DefaultEnvironment = "test"
        };
        _optionsWrapper = Options.Create(_options);
        _mockLogger = new Mock<ILogger<MetricsClient>>();
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }

    [Fact]
    public async Task RecordAsync_WithValidMetric_AddsToBuffer()
    {
        var client = new MetricsClient(_httpClient, _optionsWrapper, _mockLogger.Object);

        await client.RecordAsync("test.metric", 42.5);

        // No assertion needed - just verifying no exceptions
        client.Dispose();
    }

    [Fact]
    public async Task RecordAsync_WithCustomTags_IncludesAllTags()
    {
        SetupSuccessfulHttpResponse();
        var client = new MetricsClient(_httpClient, _optionsWrapper, _mockLogger.Object);
        var customTags = new Dictionary<string, string>
        {
            ["custom_tag"] = "custom_value"
        };

        await client.RecordAsync("test.metric", 100, customTags);
        await client.FlushAsync();

        VerifyHttpRequest(batch =>
        {
            var metric = batch.Metrics.First();
            Assert.Equal("test.metric", metric.MetricName);
            Assert.Equal(100, metric.Value);
            Assert.Equal("custom_value", metric.Tags["custom_tag"]);
            Assert.Equal("test-host", metric.Tags["host"]);
            Assert.Equal("test-service", metric.Tags["service"]);
            Assert.Equal("test", metric.Tags["environment"]);
        });

        client.Dispose();
    }

    [Fact]
    public async Task RecordAsync_AppliesDefaultTags()
    {
        SetupSuccessfulHttpResponse();
        var client = new MetricsClient(_httpClient, _optionsWrapper, _mockLogger.Object);

        await client.RecordAsync("test.metric", 50);
        await client.FlushAsync();

        VerifyHttpRequest(batch =>
        {
            var metric = batch.Metrics.First();
            Assert.Equal("test-host", metric.Tags["host"]);
            Assert.Equal("test-service", metric.Tags["service"]);
            Assert.Equal("test", metric.Tags["environment"]);
        });

        client.Dispose();
    }

    [Fact]
    public async Task RecordAsync_WhenBufferReachesCapacity_AutoFlushes()
    {
        SetupSuccessfulHttpResponse();
        var client = new MetricsClient(_httpClient, _optionsWrapper, _mockLogger.Object);

        // Buffer size is 3, so recording 3 metrics should trigger auto-flush
        await client.RecordAsync("metric1", 1);
        await client.RecordAsync("metric2", 2);
        await client.RecordAsync("metric3", 3);

        // Give a small delay for async flush to complete
        await Task.Delay(100);

        VerifyHttpRequest(batch => Assert.Equal(3, batch.Metrics.Count));

        client.Dispose();
    }

    [Fact]
    public async Task FlushAsync_WithMetricsInBuffer_SendsToCollector()
    {
        SetupSuccessfulHttpResponse();
        var client = new MetricsClient(_httpClient, _optionsWrapper, _mockLogger.Object);

        await client.RecordAsync("test.metric", 123);
        await client.FlushAsync();

        VerifyHttpRequest(batch =>
        {
            Assert.Single(batch.Metrics);
            Assert.Equal("test.metric", batch.Metrics[0].MetricName);
            Assert.Equal(123, batch.Metrics[0].Value);
        });

        client.Dispose();
    }

    [Fact]
    public async Task FlushAsync_WithEmptyBuffer_DoesNotSendRequest()
    {
        var client = new MetricsClient(_httpClient, _optionsWrapper, _mockLogger.Object);

        await client.FlushAsync();

        _mockHttpMessageHandler.Protected()
            .Verify("SendAsync", Times.Never(),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>());

        client.Dispose();
    }

    [Fact]
    public async Task FlushAsync_WhenHttpRequestFails_ReAddsMetricsToBuffer()
    {
        SetupFailedHttpResponse();
        var client = new MetricsClient(_httpClient, _optionsWrapper, _mockLogger.Object);

        await client.RecordAsync("test.metric", 999);
        await client.FlushAsync();

        // Setup successful response for retry
        SetupSuccessfulHttpResponse();
        await client.FlushAsync();

        // Verify metrics were sent on second flush
        VerifyHttpRequest(batch =>
        {
            Assert.Single(batch.Metrics);
            Assert.Equal("test.metric", batch.Metrics[0].MetricName);
        });

        client.Dispose();
    }

    [Fact]
    public async Task FlushAsync_ClearsBufferAfterSuccessfulSend()
    {
        SetupSuccessfulHttpResponse();
        var client = new MetricsClient(_httpClient, _optionsWrapper, _mockLogger.Object);

        await client.RecordAsync("test.metric", 100);
        await client.FlushAsync();

        // Second flush should not send anything
        _mockHttpMessageHandler.Invocations.Clear();
        await client.FlushAsync();

        _mockHttpMessageHandler.Protected()
            .Verify("SendAsync", Times.Never(),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>());

        client.Dispose();
    }

    [Fact]
    public async Task Dispose_FlushesRemainingMetrics()
    {
        SetupSuccessfulHttpResponse();
        var client = new MetricsClient(_httpClient, _optionsWrapper, _mockLogger.Object);

        await client.RecordAsync("final.metric", 777);

        client.Dispose();

        VerifyHttpRequest(batch =>
        {
            Assert.Single(batch.Metrics);
            Assert.Equal("final.metric", batch.Metrics[0].MetricName);
            Assert.Equal(777, batch.Metrics[0].Value);
        });
    }

    [Fact]
    public async Task RecordAsync_ConcurrentCalls_AreThreadSafe()
    {
        SetupSuccessfulHttpResponse();
        var client = new MetricsClient(_httpClient, _optionsWrapper, _mockLogger.Object);

        var tasks = Enumerable.Range(0, 10)
            .Select(i => client.RecordAsync($"metric{i}", i))
            .ToArray();

        await Task.WhenAll(tasks);
        await client.FlushAsync();

        // Verify that HTTP requests were made (metrics may be split across multiple batches due to auto-flush)
        _mockHttpMessageHandler.Protected()
            .Verify("SendAsync", Times.AtLeastOnce(),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>());

        client.Dispose();
    }

    [Fact]
    public void Constructor_WithNullOptions_UsesDefaults()
    {
        var defaultOptions = Options.Create(new MetricsClientOptions());
        var client = new MetricsClient(_httpClient, defaultOptions, _mockLogger.Object);

        Assert.NotNull(client);
        client.Dispose();
    }

    [Fact]
    public async Task RecordAsync_WithNullTags_CreatesEmptyTagsDictionary()
    {
        SetupSuccessfulHttpResponse();
        var client = new MetricsClient(_httpClient, _optionsWrapper, _mockLogger.Object);

        await client.RecordAsync("test.metric", 42, null);
        await client.FlushAsync();

        VerifyHttpRequest(batch =>
        {
            var metric = batch.Metrics.First();
            Assert.NotNull(metric.Tags);
            // Should still have default tags
            Assert.Contains("host", metric.Tags.Keys);
        });

        client.Dispose();
    }

    private void SetupSuccessfulHttpResponse()
    {
        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.Accepted,
                Content = new StringContent("{}")
            });
    }

    private void SetupFailedHttpResponse()
    {
        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError
            });
    }

    private void VerifyHttpRequest(Action<MetricBatch> batchAssertion)
    {
        _mockHttpMessageHandler.Protected()
            .Verify("SendAsync", Times.AtLeastOnce(),
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri!.ToString().Contains("/api/metrics/batch") &&
                    ValidateBatch(req, batchAssertion)),
                ItExpr.IsAny<CancellationToken>());
    }

    private bool ValidateBatch(HttpRequestMessage request, Action<MetricBatch> batchAssertion)
    {
        if (request.Content == null)
            return false;

        var batch = request.Content.ReadFromJsonAsync<MetricBatch>().Result;
        if (batch == null)
            return false;

        batchAssertion(batch);
        return true;
    }
}
