using Faro.AlertingEngine.Models;

namespace Faro.AlertingEngine.Tests.Models;

public class AlertRuleTests
{
    [Fact]
    public void AlertRule_DefaultConstructor_InitializesWithDefaults()
    {
        var rule = new AlertRule();

        Assert.Empty(rule.Id);
        Assert.Empty(rule.Name);
        Assert.Empty(rule.Description);
        Assert.Empty(rule.Query);
        Assert.Equal(TimeSpan.FromMinutes(1), rule.EvaluationInterval);
        Assert.Equal(TimeSpan.FromMinutes(5), rule.For);
        Assert.NotNull(rule.NotificationChannels);
        Assert.Empty(rule.NotificationChannels);
        Assert.NotNull(rule.Labels);
        Assert.Empty(rule.Labels);
        Assert.True(rule.Enabled);
        Assert.NotNull(rule.Condition);
    }

    [Fact]
    public void AlertRule_AllPropertiesSet_StoresCorrectly()
    {
        var rule = new AlertRule
        {
            Id = "test-rule",
            Name = "Test Rule",
            Description = "Test Description",
            Query = "SELECT 1",
            EvaluationInterval = TimeSpan.FromSeconds(30),
            For = TimeSpan.FromMinutes(2),
            NotificationChannels = ["email", "slack"],
            Labels = new Dictionary<string, string> { ["severity"] = "critical" },
            Enabled = false,
            Condition = new AlertCondition
            {
                Operator = ComparisonOperator.GreaterThan,
                Threshold = 100.0
            }
        };

        Assert.Equal("test-rule", rule.Id);
        Assert.Equal("Test Rule", rule.Name);
        Assert.Equal("Test Description", rule.Description);
        Assert.Equal("SELECT 1", rule.Query);
        Assert.Equal(TimeSpan.FromSeconds(30), rule.EvaluationInterval);
        Assert.Equal(TimeSpan.FromMinutes(2), rule.For);
        Assert.Equal(2, rule.NotificationChannels.Count);
        Assert.Contains("email", rule.NotificationChannels);
        Assert.Contains("slack", rule.NotificationChannels);
        Assert.Equal("critical", rule.Labels["severity"]);
        Assert.False(rule.Enabled);
        Assert.Equal(ComparisonOperator.GreaterThan, rule.Condition.Operator);
        Assert.Equal(100.0, rule.Condition.Threshold);
    }

    [Theory]
    [InlineData(ComparisonOperator.GreaterThan)]
    [InlineData(ComparisonOperator.LessThan)]
    [InlineData(ComparisonOperator.GreaterThanOrEqual)]
    [InlineData(ComparisonOperator.LessThanOrEqual)]
    [InlineData(ComparisonOperator.Equal)]
    [InlineData(ComparisonOperator.NotEqual)]
    public void AlertCondition_AllOperatorsSupported(ComparisonOperator op)
    {
        var condition = new AlertCondition
        {
            Operator = op,
            Threshold = 50.0
        };

        Assert.Equal(op, condition.Operator);
        Assert.Equal(50.0, condition.Threshold);
    }

    [Fact]
    public void AlertCondition_DefaultConstructor_InitializesOperator()
    {
        var condition = new AlertCondition();

        Assert.Equal(ComparisonOperator.GreaterThan, condition.Operator);
        Assert.Equal(0.0, condition.Threshold);
    }
}

public class AlertInstanceTests
{
    [Fact]
    public void AlertInstance_DefaultConstructor_InitializesWithDefaults()
    {
        var instance = new AlertInstance();

        Assert.Empty(instance.RuleId);
        Assert.Equal(AlertState.OK, instance.State);
        Assert.Null(instance.CurrentValue);
        Assert.Null(instance.FirstFiredAt);
        Assert.Null(instance.LastEvaluatedAt);
        Assert.Null(instance.LastNotifiedAt);
        Assert.Equal(0, instance.ConsecutiveFailures);
        Assert.NotNull(instance.Labels);
        Assert.Empty(instance.Labels);
    }

    [Fact]
    public void AlertInstance_StateTransitions_UpdateCorrectly()
    {
        var instance = new AlertInstance { RuleId = "test" };

        instance.State = AlertState.Pending;
        Assert.Equal(AlertState.Pending, instance.State);

        instance.State = AlertState.Firing;
        Assert.Equal(AlertState.Firing, instance.State);

        instance.State = AlertState.Resolved;
        Assert.Equal(AlertState.Resolved, instance.State);

        instance.State = AlertState.OK;
        Assert.Equal(AlertState.OK, instance.State);
    }

    [Fact]
    public void AlertInstance_Timestamps_StoreCorrectly()
    {
        var now = DateTime.UtcNow;
        var instance = new AlertInstance { RuleId = "test" };

        instance.FirstFiredAt = now;
        instance.LastEvaluatedAt = now.AddMinutes(1);
        instance.LastNotifiedAt = now.AddMinutes(2);

        Assert.Equal(now, instance.FirstFiredAt);
        Assert.Equal(now.AddMinutes(1), instance.LastEvaluatedAt);
        Assert.Equal(now.AddMinutes(2), instance.LastNotifiedAt);
    }

    [Fact]
    public void AlertInstance_Labels_CanBeModified()
    {
        var instance = new AlertInstance { RuleId = "test" };

        instance.Labels["severity"] = "high";
        instance.Labels["team"] = "platform";

        Assert.Equal(2, instance.Labels.Count);
        Assert.Equal("high", instance.Labels["severity"]);
        Assert.Equal("platform", instance.Labels["team"]);
    }
}

public class AlertStateTests
{
    [Fact]
    public void AlertState_AllStatesAvailable()
    {
        var states = Enum.GetValues<AlertState>();

        Assert.Contains(AlertState.OK, states);
        Assert.Contains(AlertState.Pending, states);
        Assert.Contains(AlertState.Firing, states);
        Assert.Contains(AlertState.Resolved, states);
    }
}
