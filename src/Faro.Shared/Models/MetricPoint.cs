namespace Faro.Shared.Models;

/// <summary>
/// Represents a single data point for a metric.
/// </summary>
public class MetricPoint
{
    /// <summary>
    /// Timestamp when the metric was recorded (UTC)
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Name of the metric (e.g., CPU_Usage, Memory_Usage)
    /// </summary>
    public string MetricName { get; set; } = string.Empty;

    /// <summary>
    /// Numeric value of the metric
    /// </summary>
    public double Value { get; set; }

    /// <summary>
    /// Key-value pairs for metric dimensions/labels
    /// </summary>
    public Dictionary<string, string> Tags { get; set; } = new();

    /// <summary>
    /// Convenience property for host tag
    /// </summary>
    public string Host => Tags.TryGetValue("host", out var host) ? host : string.Empty;

    /// <summary>
    /// Convenience property for service tag
    /// </summary>
    public string Service => Tags.TryGetValue("service", out var service) ? service : string.Empty;

    /// <summary>
    /// Convenience property for environment tag
    /// </summary>
    public string Environment => Tags.TryGetValue("environment", out var environment) ? environment : string.Empty;
}