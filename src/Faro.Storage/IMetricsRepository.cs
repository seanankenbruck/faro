using Faro.Shared.Models;

namespace Faro.Storage;

public interface IMetricsRepository
{
    /// <summary>
    /// Write a batch of metrics to storage
    /// </summary>
    Task WriteBatchAsync(IEnumerable<MetricPoint> metrics, CancellationToken cancellationToken = default);

    /// <summary>
    /// Test database connection
    /// </summary>
    Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default);
}