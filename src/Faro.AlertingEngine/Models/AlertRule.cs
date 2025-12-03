namespace Faro.AlertingEngine.Models;

public class AlertRule
{
    /// <summary>
    /// Unique identifier for the alert rule
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable name of the alert
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description of what this alert monitors
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// ClickHouse SQL query to evaluate
    /// Must return: value (numeric), timestamp (DateTime)
    /// </summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// Evaluation interval (how often to run query)
    /// </summary>
    public TimeSpan EvaluationInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Condition to trigger the alert (e.g. "value > 100")
    /// </summary>
    public AlertCondition Condition { get; set; } = new();

    /// <summary>
    /// How long condition must be true before firing alert
    /// </summary>
    public TimeSpan For { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Notification channels (e.g. email, pagerduty, sms)
    /// </summary>
    public List<string> NotificationChannels { get; set; } = [];

    /// <summary>
    /// Additional labels/tags for routing and grouping
    /// </summary>
    public Dictionary<string, string> Labels { get; set; } = [];

    /// <summary>
    /// Rule enabled status
    /// </summary>
    public bool Enabled { get; set; } = true;
}

public class AlertCondition
{
    public ComparisonOperator Operator { get; set; }
    public double Threshold { get; set; }
}

public enum ComparisonOperator
{
    GreaterThan,
    LessThan,
    GreaterThanOrEqual,
    LessThanOrEqual,
    Equal,
    NotEqual
}

public enum AlertState
{
    OK,
    Pending,
    Firing,
    Resolved
}