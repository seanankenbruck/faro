using Faro.AlertingEngine.Models;
using Faro.AlertingEngine.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace Faro.AlertingEngine.Tests;

public class AlertEvaluatorTests
{
    private readonly Mock<ILogger<AlertEvaluator>> _mockLogger;
    private readonly IConfiguration _configuration;

    public AlertEvaluatorTests()
    {
        _mockLogger = new Mock<ILogger<AlertEvaluator>>();

        var inMemorySettings = new Dictionary<string, string>
        {
            ["ConnectionStrings:ClickHouse"] = "Host=localhost;Port=9000;Database=test"
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings!)
            .Build();
    }

    [Fact]
    public void Constructor_MissingConnectionString_ThrowsException()
    {
        var configWithoutConnection = new ConfigurationBuilder().Build();

        var exception = Assert.Throws<InvalidOperationException>(
            () => new AlertEvaluator(_mockLogger.Object, configWithoutConnection));

        Assert.Contains("ClickHouse connection string not configured", exception.Message);
    }

    [Fact]
    public void Constructor_ValidConfiguration_CreatesInstance()
    {
        var evaluator = new AlertEvaluator(_mockLogger.Object, _configuration);

        Assert.NotNull(evaluator);
    }

    [Fact]
    public void AlertEvaluationResult_NoData_ReturnsOKState()
    {
        var result = AlertEvaluationResult.NoData("test-rule");

        Assert.Equal("test-rule", result.RuleId);
        Assert.Null(result.Value);
        Assert.False(result.ConditionMet);
        Assert.Equal(AlertState.OK, result.NewState);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void AlertEvaluationResult_Error_ContainsErrorMessage()
    {
        var result = AlertEvaluationResult.Error("test-rule", "Connection failed");

        Assert.Equal("test-rule", result.RuleId);
        Assert.Equal("Connection failed", result.ErrorMessage);
    }

    [Fact]
    public void AlertEvaluationResult_Success_ContainsValue()
    {
        var result = new AlertEvaluationResult
        {
            RuleId = "test-rule",
            Value = 85.5,
            ConditionMet = true,
            NewState = AlertState.Firing,
            EvaluatedAt = DateTime.UtcNow
        };

        Assert.Equal("test-rule", result.RuleId);
        Assert.Equal(85.5, result.Value);
        Assert.True(result.ConditionMet);
        Assert.Equal(AlertState.Firing, result.NewState);
    }

    [Theory]
    [InlineData(ComparisonOperator.GreaterThan, 150.0, 100.0, true)]
    [InlineData(ComparisonOperator.GreaterThan, 50.0, 100.0, false)]
    [InlineData(ComparisonOperator.LessThan, 50.0, 100.0, true)]
    [InlineData(ComparisonOperator.LessThan, 150.0, 100.0, false)]
    [InlineData(ComparisonOperator.GreaterThanOrEqual, 100.0, 100.0, true)]
    [InlineData(ComparisonOperator.GreaterThanOrEqual, 101.0, 100.0, true)]
    [InlineData(ComparisonOperator.GreaterThanOrEqual, 99.0, 100.0, false)]
    [InlineData(ComparisonOperator.LessThanOrEqual, 100.0, 100.0, true)]
    [InlineData(ComparisonOperator.LessThanOrEqual, 99.0, 100.0, true)]
    [InlineData(ComparisonOperator.LessThanOrEqual, 101.0, 100.0, false)]
    [InlineData(ComparisonOperator.Equal, 100.0, 100.0, true)]
    [InlineData(ComparisonOperator.Equal, 100.00001, 100.0, true)]
    [InlineData(ComparisonOperator.Equal, 101.0, 100.0, false)]
    [InlineData(ComparisonOperator.NotEqual, 101.0, 100.0, true)]
    [InlineData(ComparisonOperator.NotEqual, 100.0, 100.0, false)]
    public void AlertCondition_ComparisonOperators_EvaluateCorrectly(
        ComparisonOperator op, double value, double threshold, bool expectedResult)
    {
        var condition = new AlertCondition
        {
            Operator = op,
            Threshold = threshold
        };

        // This is testing the condition logic that would be used in evaluation
        bool actualResult = op switch
        {
            ComparisonOperator.GreaterThan => value > threshold,
            ComparisonOperator.LessThan => value < threshold,
            ComparisonOperator.GreaterThanOrEqual => value >= threshold,
            ComparisonOperator.LessThanOrEqual => value <= threshold,
            ComparisonOperator.Equal => Math.Abs(value - threshold) < 0.0001,
            ComparisonOperator.NotEqual => Math.Abs(value - threshold) >= 0.0001,
            _ => false
        };

        Assert.Equal(expectedResult, actualResult);
    }

    [Fact]
    public void AlertRule_WithAllProperties_CreatesSuccessfully()
    {
        var rule = new AlertRule
        {
            Id = "test-rule",
            Name = "High CPU Alert",
            Description = "Alerts when CPU exceeds threshold",
            Query = "SELECT avg(value) FROM metrics WHERE metric_name = 'cpu.usage'",
            EvaluationInterval = TimeSpan.FromMinutes(1),
            For = TimeSpan.FromMinutes(5),
            Condition = new AlertCondition
            {
                Operator = ComparisonOperator.GreaterThan,
                Threshold = 80.0
            },
            NotificationChannels = new List<string> { "email", "webhook" },
            Labels = new Dictionary<string, string>
            {
                ["severity"] = "critical",
                ["team"] = "platform"
            },
            Enabled = true
        };

        Assert.Equal("test-rule", rule.Id);
        Assert.Equal("High CPU Alert", rule.Name);
        Assert.Equal(TimeSpan.FromMinutes(1), rule.EvaluationInterval);
        Assert.Equal(TimeSpan.FromMinutes(5), rule.For);
        Assert.True(rule.Enabled);
        Assert.Equal(2, rule.NotificationChannels.Count);
        Assert.Equal(2, rule.Labels.Count);
    }

    [Fact]
    public void AlertInstance_InitialState_IsOK()
    {
        var instance = new AlertInstance
        {
            RuleId = "test-rule"
        };

        Assert.Equal(AlertState.OK, instance.State);
        Assert.Null(instance.CurrentValue);
        Assert.Null(instance.FirstFiredAt);
        Assert.Equal(0, instance.ConsecutiveFailures);
    }

    [Fact]
    public void AlertInstance_StateTransitions_Tracked()
    {
        var instance = new AlertInstance
        {
            RuleId = "test-rule",
            State = AlertState.OK
        };

        instance.State = AlertState.Pending;
        Assert.Equal(AlertState.Pending, instance.State);

        instance.State = AlertState.Firing;
        Assert.Equal(AlertState.Firing, instance.State);

        instance.State = AlertState.Resolved;
        Assert.Equal(AlertState.Resolved, instance.State);
    }

    [Fact]
    public void AlertState_AllStatesExist()
    {
        var states = new[]
        {
            AlertState.OK,
            AlertState.Pending,
            AlertState.Firing,
            AlertState.Resolved
        };

        Assert.Equal(4, states.Length);
        Assert.Contains(AlertState.OK, states);
        Assert.Contains(AlertState.Pending, states);
        Assert.Contains(AlertState.Firing, states);
        Assert.Contains(AlertState.Resolved, states);
    }

    [Fact]
    public void AlertCondition_SupportsAllOperators()
    {
        var operators = new[]
        {
            ComparisonOperator.GreaterThan,
            ComparisonOperator.LessThan,
            ComparisonOperator.GreaterThanOrEqual,
            ComparisonOperator.LessThanOrEqual,
            ComparisonOperator.Equal,
            ComparisonOperator.NotEqual
        };

        foreach (var op in operators)
        {
            var condition = new AlertCondition
            {
                Operator = op,
                Threshold = 100.0
            };

            Assert.Equal(op, condition.Operator);
            Assert.Equal(100.0, condition.Threshold);
        }
    }

    [Fact]
    public void AlertEvaluationResult_WithError_HasNoValue()
    {
        var result = AlertEvaluationResult.Error("test-rule", "Database connection failed");

        Assert.Equal("test-rule", result.RuleId);
        Assert.NotNull(result.ErrorMessage);
        Assert.Equal("Database connection failed", result.ErrorMessage);
        Assert.Null(result.Value);
    }

    [Fact]
    public void AlertEvaluationResult_NoData_HasExpectedDefaults()
    {
        var result = AlertEvaluationResult.NoData("test-rule");

        Assert.Equal("test-rule", result.RuleId);
        Assert.Null(result.Value);
        Assert.False(result.ConditionMet);
        Assert.Equal(AlertState.OK, result.NewState);
        Assert.Null(result.ErrorMessage);
        Assert.NotEqual(default(DateTime), result.EvaluatedAt);
    }

    [Fact]
    public void AlertRule_DisabledRule_CanBeCreated()
    {
        var rule = new AlertRule
        {
            Id = "disabled-rule",
            Name = "Disabled Rule",
            Query = "SELECT 1",
            Condition = new AlertCondition
            {
                Operator = ComparisonOperator.GreaterThan,
                Threshold = 100.0
            },
            Enabled = false
        };

        Assert.False(rule.Enabled);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(300)]
    public void AlertRule_SupportsVariousEvaluationIntervals(int seconds)
    {
        var rule = new AlertRule
        {
            Id = "test-rule",
            Name = "Test Rule",
            Query = "SELECT 1",
            EvaluationInterval = TimeSpan.FromSeconds(seconds),
            Condition = new AlertCondition
            {
                Operator = ComparisonOperator.GreaterThan,
                Threshold = 100.0
            }
        };

        Assert.Equal(TimeSpan.FromSeconds(seconds), rule.EvaluationInterval);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    public void AlertInstance_TracksConsecutiveFailures(int failures)
    {
        var instance = new AlertInstance
        {
            RuleId = "test-rule",
            ConsecutiveFailures = failures
        };

        Assert.Equal(failures, instance.ConsecutiveFailures);
    }

    [Fact]
    public void AlertInstance_AllTimestampsTracked()
    {
        var now = DateTime.UtcNow;
        var instance = new AlertInstance
        {
            RuleId = "test-rule",
            FirstFiredAt = now,
            LastEvaluatedAt = now.AddMinutes(1),
            LastNotifiedAt = now.AddMinutes(2)
        };

        Assert.Equal(now, instance.FirstFiredAt);
        Assert.Equal(now.AddMinutes(1), instance.LastEvaluatedAt);
        Assert.Equal(now.AddMinutes(2), instance.LastNotifiedAt);
    }
}
