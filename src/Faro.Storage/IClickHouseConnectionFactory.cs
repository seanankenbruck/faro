using ClickHouse.Client.ADO;
using Faro.Shared.Models;

namespace Faro.Storage;

public interface IClickHouseConnectionFactory
{
    /// <summary>
    /// Writes a batch of metrics to ClickHouse
    /// </summary>
    Task WriteBatchAsync(IEnumerable<MetricPoint> metrics, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a health check query against ClickHouse
    /// </summary>
    Task<bool> ExecuteHealthCheckAsync(CancellationToken cancellationToken = default);
}
