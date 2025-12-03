using Faro.AlertingEngine.Models;
using Faro.AlertingEngine.Services;

namespace Faro.AlertingEngine;

public class AlertingEngineWorker : BackgroundService
{
    private readonly ILogger<AlertingEngineWorker> _logger;
    private readonly IAlertEvaluator _evaluator;
    private readonly IAlertRuleStore _ruleStore;
    private readonly IAlertStateManager _stateManager;
    private readonly Dictionary<string, Timer> _ruleTimers = [];

    public AlertingEngineWorker(
        ILogger<AlertingEngineWorker> logger,
        IAlertEvaluator evaluator,
        IAlertRuleStore ruleStore,
        IAlertStateManager stateManager)
    {
        _logger = logger;
        _evaluator = evaluator;
        _ruleStore = ruleStore;
        _stateManager = stateManager;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Alerting Engine Worker starting");

        // Load rules and schedule evaluations
        var rules = await _ruleStore.GetAllRulesAsync();

        foreach (var rule in rules.Where(r => r.Enabled))
        {
            ScheduleRule(rule, stoppingToken);
        }

        // Keep running until cancelled
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private void ScheduleRule(AlertRule rule, CancellationToken stoppingToken)
    {
        var timer = new Timer(
            async _ => await EvaluateRuleAsync(rule),
            null,
            TimeSpan.Zero,
            rule.EvaluationInterval
        );
        
        _ruleTimers[rule.Id] = timer;
        _logger.LogInformation("Scheduled rule {RuleId} to run every {Interval}",
            rule.Id, rule.EvaluationInterval);
    }

    private async Task EvaluateRuleAsync(AlertRule rule)
    {
        try
        {
            var instance = await _stateManager.GetInstanceAsync(rule.Id);
            var result = await _evaluator.EvaluateRuleAsync(rule, instance);

            // Update instance state
            instance.CurrentValue = result.Value;
            instance.State = result.NewState;
            instance.LastEvaluatedAt = result.EvaluatedAt;

            if (result.NewState == AlertState.Pending && instance.FirstFiredAt == null)
            {
                instance.FirstFiredAt = result.EvaluatedAt;
            }

            if (result.NewState == AlertState.OK || result.NewState == AlertState.Resolved)
            {
                instance.FirstFiredAt = null;
                instance.ConsecutiveFailures = 0;
            }

            await _stateManager.SaveInstanceAsync(instance);

            _logger.LogInformation(
                "Rule {RuleId}: Value={Value}, State={State}",
                rule.Id, result.Value, result.NewState);
            
            // Send notifications if state changed to FIRING or RESOLVED
            if (ShouldNotify(instance, result.NewState))
            {
                await SendNotificationsAsync(rule, instance);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating rule {RuleId}", rule.Id);
        }
    }

    private bool ShouldNotify(AlertInstance instance, AlertState newState)
    {
        // Notify on state transitions to FIRING or RESOLVED
        return newState == AlertState.Firing || newState == AlertState.Resolved;
    }

    private async Task SendNotificationsAsync(AlertRule rule, AlertInstance instance)
    {
        // TODO: Placeholder logic for notification flow
        _logger.LogInformation(
            "Would send notification for rule {RuleId}: {State}",
            rule.Id, instance.State);
        await Task.CompletedTask;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Alerting Engine stopping...");

        foreach (var timer in _ruleTimers.Values)
        {
            timer.Dispose();
        }

        await base.StopAsync(cancellationToken);
    }
}
