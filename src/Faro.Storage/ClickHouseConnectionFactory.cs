using ClickHouse.Client.ADO;
using ClickHouse.Client.Copy;
using Faro.Shared.Models;
using Faro.Storage.Configuration;
using Microsoft.Extensions.Options;

namespace Faro.Storage;

public class ClickHouseConnectionFactory : IClickHouseConnectionFactory
{
    private readonly ClickHouseOptions _options;

    public ClickHouseConnectionFactory(IOptions<ClickHouseOptions> options)
    {
        _options = options.Value;
    }

    public async Task WriteBatchAsync(IEnumerable<MetricPoint> metrics, CancellationToken cancellationToken = default)
    {
        var metricsList = metrics.ToList();

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
    }

    public async Task<bool> ExecuteHealthCheckAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new ClickHouseConnection(_options.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1";
        var result = await command.ExecuteScalarAsync(cancellationToken);

        return result != null;
    }
}
