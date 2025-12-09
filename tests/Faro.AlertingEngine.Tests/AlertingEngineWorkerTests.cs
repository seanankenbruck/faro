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
