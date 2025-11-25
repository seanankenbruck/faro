using Faro.Shared.Models;
using Faro.Storage.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;

namespace Faro.Storage;

public class ClickHouseMetricsRepository: IMetricsRepository
{
    private readonly ClickHouseOptions _options;
    private readonly ILogger<ClickHouseMetricsRepository> _logger;
    private readonly IClickHouseConnectionFactory _connectionFactory;
    private readonly AsyncRetryPolicy _retryPolicy;

    public ClickHouseMetricsRepository(
        IOptions<ClickHouseOptions> options,
        ILogger<ClickHouseMetricsRepository> logger,
        IClickHouseConnectionFactory connectionFactory)
    {
        _options = options.Value;
        _logger = logger;
        _connectionFactory = connectionFactory;

        // configure retry policy with exponential backoff
        _retryPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(
                retryCount: _options.MaxRetries,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning(
                        exception,
                        "Retry {RetryCount} after {Delay} due to error: {Message}",
                        retryCount,
                        timeSpan.TotalMilliseconds,
                        exception.Message
                    );
                });
    }

    public async Task WriteBatchAsync(IEnumerable<MetricPoint> metrics, CancellationToken cancellationToken = default)
    {
        // Check for null batch
        if (metrics == null)
        {
            _logger.LogDebug("Null metrics batch provided.");
            return;
        }

        var metricsList = metrics.ToList();
        if (!metricsList.Any())
        {
            _logger.LogDebug("No metrics to write.");
            return;
        }

        try
        {
            await _retryPolicy.ExecuteAsync(async () =>
            {
                await _connectionFactory.WriteBatchAsync(metricsList, cancellationToken);
                _logger.LogInformation(
                    "Successfully wrote {Count} metrics to ClickHouse.",
                    metricsList.Count
                );
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to write metrics batch of size {BatchSize} after {MaxRetries} retries.",
                metricsList.Count,
                _options.MaxRetries
            );
            throw;
        }
    }

    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                return await _connectionFactory.ExecuteHealthCheckAsync(cancellationToken);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ClickHouse health check failed.");
            return false;
        }
    }
}