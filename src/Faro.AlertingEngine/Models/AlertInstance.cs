namespace Faro.AlertingEngine.Models;

/// <summary>
/// Represents the runtime state of an alert rule evaluation
/// </summary>
public class AlertInstance
{
    public string RuleId { get; set; } = string.Empty;
    public AlertState State { get; set; } = AlertState.OK;
    public double? CurrentValue { get; set; }
    public DateTime? FirstFiredAt { get; set; }
    public DateTime? LastEvaluatedAt { get; set; }
    public DateTime? LastNotifiedAt { get; set; }
    public int ConsecutiveFailures { get; set; }

    /// <summary>
    /// Additional context from the query result
    /// </summary>
    public Dictionary<string, string> Labels { get; set; } = [];
}