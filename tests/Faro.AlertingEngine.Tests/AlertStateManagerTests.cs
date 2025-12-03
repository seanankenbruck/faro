using Faro.AlertingEngine.Models;
using Faro.AlertingEngine.Services;

namespace Faro.AlertingEngine.Tests;

public class AlertStateManagerTests
{
    [Fact]
    public async Task GetInstanceAsync_NewRule_CreatesInstance()
    {
        var manager = new InMemoryAlertStateManager();

        var instance = await manager.GetInstanceAsync("test-rule");

        Assert.NotNull(instance);
        Assert.Equal("test-rule", instance.RuleId);
        Assert.Equal(AlertState.OK, instance.State);
        Assert.Null(instance.CurrentValue);
        Assert.Null(instance.FirstFiredAt);
        Assert.Null(instance.LastEvaluatedAt);
        Assert.Equal(0, instance.ConsecutiveFailures);
    }

    [Fact]
    public async Task GetInstanceAsync_SameRule_ReturnsSameInstance()
    {
        var manager = new InMemoryAlertStateManager();

        var instance1 = await manager.GetInstanceAsync("test-rule");
        var instance2 = await manager.GetInstanceAsync("test-rule");

        Assert.Same(instance1, instance2);
    }

    [Fact]
    public async Task SaveInstanceAsync_PersistsChanges()
    {
        var manager = new InMemoryAlertStateManager();
        var instance = await manager.GetInstanceAsync("test-rule");

        instance.State = AlertState.Firing;
        instance.CurrentValue = 95.5;
        instance.FirstFiredAt = DateTime.UtcNow;
        instance.ConsecutiveFailures = 3;

        await manager.SaveInstanceAsync(instance);

        var retrieved = await manager.GetInstanceAsync("test-rule");

        Assert.Equal(AlertState.Firing, retrieved.State);
        Assert.Equal(95.5, retrieved.CurrentValue);
        Assert.NotNull(retrieved.FirstFiredAt);
        Assert.Equal(3, retrieved.ConsecutiveFailures);
    }

    [Fact]
    public async Task SaveInstanceAsync_ExistingInstance_Updates()
    {
        var manager = new InMemoryAlertStateManager();
        var instance = await manager.GetInstanceAsync("test-rule");

        instance.State = AlertState.Pending;
        await manager.SaveInstanceAsync(instance);

        instance.State = AlertState.Firing;
        await manager.SaveInstanceAsync(instance);

        var retrieved = await manager.GetInstanceAsync("test-rule");

        Assert.Equal(AlertState.Firing, retrieved.State);
    }

    [Fact]
    public async Task GetInstanceAsync_MultipleRules_IsolatesState()
    {
        var manager = new InMemoryAlertStateManager();

        var instance1 = await manager.GetInstanceAsync("rule-1");
        var instance2 = await manager.GetInstanceAsync("rule-2");
        var instance3 = await manager.GetInstanceAsync("rule-3");

        instance1.State = AlertState.OK;
        instance2.State = AlertState.Firing;
        instance3.State = AlertState.Pending;

        await manager.SaveInstanceAsync(instance1);
        await manager.SaveInstanceAsync(instance2);
        await manager.SaveInstanceAsync(instance3);

        var retrieved1 = await manager.GetInstanceAsync("rule-1");
        var retrieved2 = await manager.GetInstanceAsync("rule-2");
        var retrieved3 = await manager.GetInstanceAsync("rule-3");

        Assert.Equal(AlertState.OK, retrieved1.State);
        Assert.Equal(AlertState.Firing, retrieved2.State);
        Assert.Equal(AlertState.Pending, retrieved3.State);
    }

    [Fact]
    public async Task SaveInstanceAsync_ConcurrentWrites_HandledSafely()
    {
        var manager = new InMemoryAlertStateManager();
        var instance = await manager.GetInstanceAsync("test-rule");

        var tasks = Enumerable.Range(1, 10).Select(async i =>
        {
            instance.ConsecutiveFailures = i;
            await manager.SaveInstanceAsync(instance);
        });

        await Task.WhenAll(tasks);

        var retrieved = await manager.GetInstanceAsync("test-rule");

        Assert.InRange(retrieved.ConsecutiveFailures, 1, 10);
    }

    [Fact]
    public async Task SaveInstanceAsync_AllProperties_Preserved()
    {
        var manager = new InMemoryAlertStateManager();
        var now = DateTime.UtcNow;
        var instance = await manager.GetInstanceAsync("test-rule");

        instance.State = AlertState.Firing;
        instance.CurrentValue = 85.7;
        instance.FirstFiredAt = now;
        instance.LastEvaluatedAt = now.AddMinutes(1);
        instance.LastNotifiedAt = now.AddMinutes(2);
        instance.ConsecutiveFailures = 5;
        instance.Labels = new Dictionary<string, string>
        {
            ["severity"] = "critical",
            ["team"] = "platform"
        };

        await manager.SaveInstanceAsync(instance);

        var retrieved = await manager.GetInstanceAsync("test-rule");

        Assert.Equal(AlertState.Firing, retrieved.State);
        Assert.Equal(85.7, retrieved.CurrentValue);
        Assert.Equal(now, retrieved.FirstFiredAt);
        Assert.Equal(now.AddMinutes(1), retrieved.LastEvaluatedAt);
        Assert.Equal(now.AddMinutes(2), retrieved.LastNotifiedAt);
        Assert.Equal(5, retrieved.ConsecutiveFailures);
        Assert.Equal(2, retrieved.Labels.Count);
        Assert.Equal("critical", retrieved.Labels["severity"]);
        Assert.Equal("platform", retrieved.Labels["team"]);
    }

    [Fact]
    public async Task GetInstanceAsync_ConcurrentAccess_ThreadSafe()
    {
        var manager = new InMemoryAlertStateManager();

        var tasks = Enumerable.Range(1, 100).Select(async _ =>
        {
            var instance = await manager.GetInstanceAsync("concurrent-rule");
            return instance;
        });

        var instances = await Task.WhenAll(tasks);

        Assert.All(instances, i => Assert.Equal("concurrent-rule", i.RuleId));
        Assert.Single(instances.Distinct());
    }
}
