using System.Collections.Concurrent;
using Faro.AlertingEngine.Models;

namespace Faro.AlertingEngine.Services;

public interface IAlertStateManager
{
    Task<AlertInstance> GetInstanceAsync(string ruleId);
    Task SaveInstanceAsync(AlertInstance instance);
}

public class InMemoryAlertStateManager: IAlertStateManager
{
    private readonly ConcurrentDictionary<string, AlertInstance> _instances = [];

    public Task<AlertInstance> GetInstanceAsync(string ruleId)
    {
        var instance = _instances.GetOrAdd(ruleId, id => new AlertInstance { RuleId = id });
        return Task.FromResult(instance);
    }

    public Task SaveInstanceAsync(AlertInstance instance)
    {
        _instances[instance.RuleId] = instance;
        return Task.CompletedTask;
    }
}