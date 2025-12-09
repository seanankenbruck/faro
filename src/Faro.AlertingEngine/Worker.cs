using Faro.AlertingEngine.Models;
using Faro.AlertingEngine.Services;
using Faro.Notifications;

namespace Faro.AlertingEngine;

public class AlertingEngineWorker : BackgroundService
{
    private readonly ILogger<AlertingEngineWorker> _logger;
    private readonly IAlertEvaluator _evaluator;
    private readonly IAlertRuleStore _ruleStore;
    private readonly IAlertStateManager _stateManager;
    private readonly IEnumerable<INotificationChannel> _notificationChannels;
    private readonly Dictionary<string, Timer> _ruleTimers = [];

    public AlertingEngineWorker(
        ILogger<AlertingEngineWorker> logger,
        IAlertEvaluator evaluator,
        IAlertRuleStore ruleStore,
        IAlertStateManager stateManager,
        IEnumerable<INotificationChannel> notificationChannels)
    {
        _logger = logger;
        _evaluator = evaluator;
        _ruleStore = ruleStore;
        _stateManager = stateManager;
        _notificationChannels = notificationChannels;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Alerting Engine Worker starting");

        // Load rules and schedule evaluations
        var rules = await _ruleStore.GetAllRulesAsync();

        _logger.LogInformation("Loaded {Count} alert rules", rules.Count);

        foreach (var rule in rules.Where(r => r.Enabled))
        {
            _logger.LogInformation("Loading rule: Id={Id}, Name={Name}, Query={Query}, Enabled={Enabled}",
                rule.Id, rule.Name, rule.Query, rule.Enabled);
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

            // Capture previous state before updating
            var previousState = instance.State;

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
            var shouldNotify = ShouldNotify(previousState, result.NewState);
            _logger.LogInformation(
                "Rule {RuleId}: ShouldNotify={ShouldNotify}, PreviousState={PreviousState}, NewState={NewState}",
                rule.Id, shouldNotify, previousState, result.NewState);

            if (shouldNotify)
            {
                _logger.LogInformation("Sending notifications for rule {RuleId}", rule.Id);
                await SendNotificationsAsync(rule, instance);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating rule {RuleId}", rule.Id);
        }
    }

    private bool ShouldNotify(AlertState previousState, AlertState newState)
    {
        // Notify only on state transitions to FIRING or RESOLVED
        return previousState != newState &&
               (newState == AlertState.Firing || newState == AlertState.Resolved);
    }

    private async Task SendNotificationsAsync(AlertRule rule, AlertInstance instance)
    {
        _logger.LogInformation(
            "SendNotificationsAsync called for rule {RuleId}, channels: {Channels}",
            rule.Id, string.Join(", ", rule.NotificationChannels));

        _logger.LogInformation(
            "Available notification channels: {AvailableChannels}",
            string.Join(", ", _notificationChannels.Select(c => c.Name)));

        var message = new NotificationMessage
        {
            Title = $"{rule.Name} - {instance.State}",
            Body = BuildNotificationBody(rule, instance),
            Severity = DetermineSeverity(rule.Labels),
            Metadata = new Dictionary<string, string>
            {
                ["rule_id"] = rule.Id,
                ["state"] = instance.State.ToString(),
                ["value"] = instance.CurrentValue?.ToString() ?? "N/A"
            }
        };

        foreach (var channelName in rule.NotificationChannels)
        {
            var channel = _notificationChannels.FirstOrDefault(c => c.Name == channelName);
            if (channel != null)
            {
                try
                {
                    _logger.LogInformation("Sending notification via {Channel}", channelName);
                    await channel.SendAsync(message);
                    _logger.LogInformation("Successfully sent notification via {Channel}", channelName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send notification via {Channel}", channelName);
                }
            }
            else
            {
                _logger.LogWarning("Notification channel {ChannelName} not found", channelName);
            }
        }

        instance.LastNotifiedAt = DateTime.UtcNow;
    }

    private string BuildNotificationBody(AlertRule rule, AlertInstance instance)
    {
        return $@"
        Alert: {rule.Name}
        Description: {rule.Description}
        State: {instance.State}
        Current Value: {instance.CurrentValue}
        Threshold: {rule.Condition.Operator} {rule.Condition.Threshold}
        First Detected: {instance.FirstFiredAt}
        Last Evaluated: {instance.LastEvaluatedAt}
        ";
    }

    private static NotificationSeverity DetermineSeverity(Dictionary<string, string> labels)
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
