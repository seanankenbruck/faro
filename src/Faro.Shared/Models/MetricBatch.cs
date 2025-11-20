using Faro.Shared.Models;

namespace Faro.Shared.Models;

/// <summary>
/// Represents a batch of metrics for efficient transmission
/// </summary>
public class MetricBatch
{
    /// <summary>
    /// Collection of metric points in this batch
    /// </summary>
    public List<MetricPoint> Metrics { get; set; } = new();

    /// <summary>
    /// Timestamp when the batch was created (UTC)
    /// </summary>
    public DateTime BatchTimestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Optional source identifier
    /// </summary>
    public string? SourceId { get; set; }
}