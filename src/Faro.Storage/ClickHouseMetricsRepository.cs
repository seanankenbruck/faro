using ClickHouse.Client.ADO;
using ClickHouse.Client.Copy;
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
    private readonly AsyncRetryPolicy _retryPolicy;

    public ClickHouseMetricsRepository(
        IOptions<ClickHouseOptions> options,
        ILogger<ClickHouseMetricsRepository> logger)
    {
        _options = options.Value;
        _logger = logger;

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
                await using var connection = new ClickHouseConnection(_options.ConnectionString);
                await connection.OpenAsync(cancellationToken);

                using var bulkCopy = new ClickHouseBulkCopy(connection)
                {
                    DestinationTableName = "metrics",
                    BatchSize = 10000,
                    MaxDegreeOfParallelism = 4
                };

                await bulkCopy.InitAsync();

                var rows = metricsList.Select(m => new object[]
                {
                    m.Timestamp,
                    m.MetricName,
                    m.Value,
                    m.Tags,
                    m.Host,
                    m.Service,
                    m.Environment
                }).ToList();

                await bulkCopy.WriteToServerAsync(rows, cancellationToken);
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
                await using var connection = new ClickHouseConnection(_options.ConnectionString);
                await connection.OpenAsync(cancellationToken);

                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT 1";
                var result = await command.ExecuteScalarAsync(cancellationToken);

                return result != null;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ClickHouse health check failed.");
            return false;
        }
    }
}