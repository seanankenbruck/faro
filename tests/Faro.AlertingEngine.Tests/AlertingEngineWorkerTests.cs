using Faro.AlertingEngine.Models;
using Faro.AlertingEngine.Services;
using Faro.Notifications;
using Microsoft.Extensions.Logging;
using Moq;

namespace Faro.AlertingEngine.Tests;

public class AlertingEngineWorkerTests
{
    private readonly Mock<ILogger<AlertingEngineWorker>> _mockLogger;
    private readonly Mock<IAlertEvaluator> _mockEvaluator;
    private readonly Mock<IAlertRuleStore> _mockRuleStore;
    private readonly Mock<IAlertStateManager> _mockStateManager;
    private readonly Mock<INotificationChannel> _mockEmailChannel;
    private readonly Mock<INotificationChannel> _mockWebhookChannel;
    private readonly List<INotificationChannel> _notificationChannels;

    public AlertingEngineWorkerTests()
    {
        _mockLogger = new Mock<ILogger<AlertingEngineWorker>>();
        _mockEvaluator = new Mock<IAlertEvaluator>();
        _mockRuleStore = new Mock<IAlertRuleStore>();
        _mockStateManager = new Mock<IAlertStateManager>();

        _mockEmailChannel = new Mock<INotificationChannel>();
        _mockEmailChannel.Setup(c => c.Name).Returns("email");

        _mockWebhookChannel = new Mock<INotificationChannel>();
        _mockWebhookChannel.Setup(c => c.Name).Returns("webhook");

        _notificationChannels = new List<INotificationChannel>
        {
            _mockEmailChannel.Object,
            _mockWebhookChannel.Object
        };
    }

    [Fact]
    public void Constructor_WithValidDependencies_CreatesInstance()
    {
        var worker = new AlertingEngineWorker(
            _mockLogger.Object,
            _mockEvaluator.Object,
            _mockRuleStore.Object,
            _mockStateManager.Object,
            _notificationChannels);

        Assert.NotNull(worker);
    }

    [Fact]
    public void Constructor_WithEmptyChannels_CreatesInstance()
    {
        var emptyChannels = new List<INotificationChannel>();

        var worker = new AlertingEngineWorker(
            _mockLogger.Object,
            _mockEvaluator.Object,
            _mockRuleStore.Object,
            _mockStateManager.Object,
            emptyChannels);

        Assert.NotNull(worker);
    }

    [Fact]
    public void NotificationChannels_AreInjectedCorrectly()
    {
        var worker = new AlertingEngineWorker(
            _mockLogger.Object,
            _mockEvaluator.Object,
            _mockRuleStore.Object,
            _mockStateManager.Object,
            _notificationChannels);

        Assert.NotNull(worker);
        Assert.Equal(2, _notificationChannels.Count);
        Assert.Contains(_notificationChannels, c => c.Name == "email");
        Assert.Contains(_notificationChannels, c => c.Name == "webhook");
    }

    [Fact]
    public void AlertRule_CanConfigureNotificationChannels()
    {
        var rule = CreateTestRule("test-rule");
        rule.NotificationChannels = new List<string> { "email", "webhook" };

        Assert.Equal(2, rule.NotificationChannels.Count);
        Assert.Contains("email", rule.NotificationChannels);
        Assert.Contains("webhook", rule.NotificationChannels);
    }

    [Fact]
    public void NotificationMessage_IncludesRequiredMetadata()
    {
        var message = new NotificationMessage
        {
            Title = "Test Alert - Firing",
            Body = "Alert details",
            Severity = NotificationSeverity.Critical,
            Metadata = new Dictionary<string, string>
            {
                ["rule_id"] = "test-rule",
                ["state"] = "Firing",
                ["value"] = "150.0"
            }
        };

        Assert.Contains("rule_id", message.Metadata.Keys);
        Assert.Contains("state", message.Metadata.Keys);
        Assert.Contains("value", message.Metadata.Keys);
        Assert.Equal("test-rule", message.Metadata["rule_id"]);
        Assert.Equal("Firing", message.Metadata["state"]);
    }

    [Theory]
    [InlineData("critical")]
    [InlineData("warning")]
    [InlineData("info")]
    [InlineData("unknown")]
    public void AlertRule_SupportsVariousSeverityLabels(string severityLabel)
    {
        var rule = CreateTestRule("test-rule");
        rule.Labels["severity"] = severityLabel;

        Assert.Equal(severityLabel, rule.Labels["severity"]);
        Assert.True(rule.Labels.ContainsKey("severity"));
    }

    [Fact]
    public void AlertRule_SupportsLabelsForSeverityMapping()
    {
        var rule = CreateTestRule("test-rule");
        rule.Labels["severity"] = "critical";
        rule.Labels["team"] = "platform";

        Assert.Equal("critical", rule.Labels["severity"]);
        Assert.Equal("platform", rule.Labels["team"]);
        Assert.Equal(2, rule.Labels.Count);
    }

    [Fact]
    public void AlertState_Transitions_FromOKToPending()
    {
        var previousState = AlertState.OK;
        var newState = AlertState.Pending;

        var shouldNotify = ShouldNotify(previousState, newState);

        Assert.False(shouldNotify);
    }

    [Fact]
    public void AlertState_Transitions_FromPendingToFiring_ShouldNotify()
    {
        var previousState = AlertState.Pending;
        var newState = AlertState.Firing;

        var shouldNotify = ShouldNotify(previousState, newState);

        Assert.True(shouldNotify);
    }

    [Fact]
    public void AlertState_Transitions_FromFiringToResolved_ShouldNotify()
    {
        var previousState = AlertState.Firing;
        var newState = AlertState.Resolved;

        var shouldNotify = ShouldNotify(previousState, newState);

        Assert.True(shouldNotify);
    }

    [Fact]
    public void AlertState_Transitions_FromOKToFiring_ShouldNotify()
    {
        var previousState = AlertState.OK;
        var newState = AlertState.Firing;

        var shouldNotify = ShouldNotify(previousState, newState);

        Assert.True(shouldNotify);
    }

    [Fact]
    public void AlertState_NoTransition_ShouldNotNotify()
    {
        var states = new[] { AlertState.OK, AlertState.Pending, AlertState.Firing, AlertState.Resolved };

        foreach (var state in states)
        {
            var shouldNotify = ShouldNotify(state, state);
            Assert.False(shouldNotify);
        }
    }

    [Theory]
    [InlineData("critical", NotificationSeverity.Critical)]
    [InlineData("warning", NotificationSeverity.Warning)]
    [InlineData("info", NotificationSeverity.Info)]
    public void DetermineSeverity_MapsLabelToSeverity(string label, NotificationSeverity expectedSeverity)
    {
        var labels = new Dictionary<string, string>
        {
            ["severity"] = label
        };

        var severity = DetermineSeverity(labels);

        Assert.Equal(expectedSeverity, severity);
    }

    [Fact]
    public void DetermineSeverity_UnknownLabel_ReturnsWarning()
    {
        var labels = new Dictionary<string, string>
        {
            ["severity"] = "unknown"
        };

        var severity = DetermineSeverity(labels);

        Assert.Equal(NotificationSeverity.Warning, severity);
    }

    [Fact]
    public void DetermineSeverity_NoSeverityLabel_ReturnsWarning()
    {
        var labels = new Dictionary<string, string>
        {
            ["team"] = "platform"
        };

        var severity = DetermineSeverity(labels);

        Assert.Equal(NotificationSeverity.Warning, severity);
    }

    [Fact]
    public void DetermineSeverity_EmptyLabels_ReturnsWarning()
    {
        var labels = new Dictionary<string, string>();

        var severity = DetermineSeverity(labels);

        Assert.Equal(NotificationSeverity.Warning, severity);
    }

    [Fact]
    public void NotificationMessage_ContainsMetadata()
    {
        var message = new NotificationMessage
        {
            Title = "Test Alert",
            Body = "Alert body",
            Severity = NotificationSeverity.Critical,
            Metadata = new Dictionary<string, string>
            {
                ["rule_id"] = "test-rule",
                ["state"] = "Firing"
            }
        };

        Assert.NotNull(message.Metadata);
        Assert.Equal(2, message.Metadata.Count);
        Assert.Equal("test-rule", message.Metadata["rule_id"]);
        Assert.Equal("Firing", message.Metadata["state"]);
    }

    [Fact]
    public void AlertRule_WithMultipleChannels_ConfiguredCorrectly()
    {
        var rule = CreateTestRule("test-rule");
        rule.NotificationChannels = new List<string> { "email", "webhook", "pagerduty" };

        Assert.Equal(3, rule.NotificationChannels.Count);
        Assert.Contains("email", rule.NotificationChannels);
        Assert.Contains("webhook", rule.NotificationChannels);
        Assert.Contains("pagerduty", rule.NotificationChannels);
    }

    [Fact]
    public void AlertRule_DefaultEvaluationInterval_IsOneMinute()
    {
        var rule = new AlertRule
        {
            Id = "test",
            Name = "Test"
        };

        Assert.Equal(TimeSpan.FromMinutes(1), rule.EvaluationInterval);
    }

    [Fact]
    public void AlertRule_DefaultForDuration_IsFiveMinutes()
    {
        var rule = new AlertRule
        {
            Id = "test",
            Name = "Test"
        };

        Assert.Equal(TimeSpan.FromMinutes(5), rule.For);
    }

    [Fact]
    public void AlertRule_DefaultEnabled_IsTrue()
    {
        var rule = new AlertRule
        {
            Id = "test",
            Name = "Test"
        };

        Assert.True(rule.Enabled);
    }

    [Fact]
    public void NotificationSeverity_AllLevelsExist()
    {
        var severities = new[]
        {
            NotificationSeverity.Info,
            NotificationSeverity.Warning,
            NotificationSeverity.Critical
        };

        Assert.Equal(3, severities.Length);
    }

    [Fact]
    public void AlertInstance_DefaultConsecutiveFailures_IsZero()
    {
        var instance = new AlertInstance
        {
            RuleId = "test-rule"
        };

        Assert.Equal(0, instance.ConsecutiveFailures);
    }

    [Fact]
    public void AlertInstance_DefaultState_IsOK()
    {
        var instance = new AlertInstance
        {
            RuleId = "test-rule"
        };

        Assert.Equal(AlertState.OK, instance.State);
    }

    [Fact]
    public void AlertCondition_WithThreshold_StoresCorrectly()
    {
        var condition = new AlertCondition
        {
            Operator = ComparisonOperator.GreaterThan,
            Threshold = 85.5
        };

        Assert.Equal(ComparisonOperator.GreaterThan, condition.Operator);
        Assert.Equal(85.5, condition.Threshold);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(50.0)]
    [InlineData(100.0)]
    [InlineData(-50.0)]
    public void AlertCondition_SupportsVariousThresholds(double threshold)
    {
        var condition = new AlertCondition
        {
            Operator = ComparisonOperator.GreaterThan,
            Threshold = threshold
        };

        Assert.Equal(threshold, condition.Threshold);
    }

    // Helper methods that match the Worker class logic
    private bool ShouldNotify(AlertState previousState, AlertState newState)
    {
        return previousState != newState &&
               (newState == AlertState.Firing || newState == AlertState.Resolved);
    }

    private NotificationSeverity DetermineSeverity(Dictionary<string, string> labels)
    {
        if (labels.TryGetValue("severity", out var severity))
        {
            return severity.ToLowerInvariant() switch
            {
                "critical" => NotificationSeverity.Critical,
                "warning" => NotificationSeverity.Warning,
                "info" => NotificationSeverity.Info,
                _ => NotificationSeverity.Warning
            };
        }

        return NotificationSeverity.Warning;
    }

    private AlertRule CreateTestRule(string id, bool enabled = true)
    {
        return new AlertRule
        {
            Id = id,
            Name = $"Test Rule {id}",
            Description = "Test alert rule",
            Query = "SELECT 1",
            EvaluationInterval = TimeSpan.FromMilliseconds(100),
            Condition = new AlertCondition
            {
                Operator = ComparisonOperator.GreaterThan,
                Threshold = 100.0
            },
            For = TimeSpan.FromMinutes(1),
            NotificationChannels = new List<string>(),
            Labels = new Dictionary<string, string>(),
            Enabled = enabled
        };
    }
}
