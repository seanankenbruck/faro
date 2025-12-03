using ClickHouse.Client.ADO;
using Faro.AlertingEngine.Models;
using Microsoft.Extensions.Logging;

namespace Faro.AlertingEngine.Services;

public interface IAlertEvaluator
{
    Task<AlertEvaluationResult> EvaluateRuleAsync(AlertRule rule, AlertInstance instance);
}

public class AlertEvaluator: IAlertEvaluator
{
    private readonly ILogger<AlertEvaluator> _logger;
    private readonly string _connectionString;

    public AlertEvaluator(ILogger<AlertEvaluator> logger, IConfiguration configuration)
    {
        _logger = logger;
        _connectionString = configuration.GetConnectionString("ClickHouse")
            ?? throw new InvalidOperationException("ClickHouse connection string not configured");
    }

    public async Task<AlertEvaluationResult> EvaluateRuleAsync(AlertRule rule, AlertInstance instance)
    {
        try
        {
            // Execute the query
            var queryResult = await ExecuteQueryAsync(rule.Query);

            if (!queryResult.HasValue)
            {
                _logger.LogWarning("Query for rule {RuleId} returned no data", rule.Id);
                return AlertEvaluationResult.NoData(rule.Id);
            }

            // Evaluate the condition
            bool conditionMet = EvaluateCondition(queryResult.Value, rule.Condition);

            // Update state
            var newState = DetermineNewState(instance, conditionMet, rule.For);

            return new AlertEvaluationResult
            {
                RuleId = rule.Id,
                Value = queryResult.Value,
                ConditionMet = conditionMet,
                NewState = newState,
                EvaluatedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating rule {RuleId}", rule.Id);
            return AlertEvaluationResult.Error(rule.Id, ex.Message);
        }
    }

    private async Task<double?> ExecuteQueryAsync(string query)
    {
        using var connection = new ClickHouseConnection(_connectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = query;

        var result = await command.ExecuteScalarAsync();

        return result switch
        {
            null => null,
            double d => d,
            float f => (double)f,
            int i => (double)i,
            long l => (double)l,
            _ => throw new InvalidOperationException("Unexpected query result type")
        };
    }

    private bool EvaluateCondition(double value, AlertCondition condition)
    {
        return condition.Operator switch
        {
            ComparisonOperator.GreaterThan => value > condition.Threshold,
            ComparisonOperator.LessThan => value < condition.Threshold,
            ComparisonOperator.GreaterThanOrEqual => value >= condition.Threshold,
            ComparisonOperator.LessThanOrEqual => value <= condition.Threshold,
            ComparisonOperator.Equal => Math.Abs(value - condition.Threshold) < 0.0001,
            ComparisonOperator.NotEqual => Math.Abs(value - condition.Threshold) >= 0.0001,
            _ => throw new InvalidOperationException("Unsupported comparison operator")
        };
    }

    private AlertState DetermineNewState(AlertInstance instance, bool conditionMet, TimeSpan forDuration)
    {
        if (!conditionMet)
        {
            // Condition not met, return to OK if was firing
            return instance.State == AlertState.Firing ? AlertState.Resolved : AlertState.OK;
        }

        // Condition met
        if (instance.State == AlertState.OK || instance.State == AlertState.Resolved)
        {
            // First time condition met, move to Pending
            return AlertState.Pending;
        }

        if (instance.State == AlertState.Pending)
        {
            // Check if 'for' duration has passed
            var pendingDuration = DateTime.UtcNow - (instance.FirstFiredAt ?? DateTime.UtcNow);
            return pendingDuration >= forDuration ? AlertState.Firing: AlertState.Pending;
        }

        // Already firing, return Firing
        return AlertState.Firing;
    }
}

public class AlertEvaluationResult
{
    public string RuleId { get; set; } = string.Empty;
    public double? Value { get; set; }
    public bool ConditionMet { get; set; }
    public AlertState NewState { get; set; }
    public DateTime EvaluatedAt { get; set; }
    public string? ErrorMessage { get; set; }

    public static AlertEvaluationResult NoData(string ruleId) => new()
    {
        RuleId = ruleId,
        Value = null,
        ConditionMet = false,
        NewState = AlertState.OK,
        EvaluatedAt = DateTime.UtcNow
    };

    public static AlertEvaluationResult Error(string ruleId, string error) => new()
    {
        RuleId = ruleId,
        ErrorMessage = error,
        EvaluatedAt = DateTime.UtcNow
    };
}