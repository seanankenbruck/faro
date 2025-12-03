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
}
