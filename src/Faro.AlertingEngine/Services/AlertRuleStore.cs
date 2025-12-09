using System.Text.Json;
using Faro.AlertingEngine.Models;

namespace Faro.AlertingEngine.Services;

public interface IAlertRuleStore
{
    Task<List<AlertRule>> GetAllRulesAsync();
    Task<AlertRule?> GetRuleAsync(string id);
    Task SaveRuleAsync(AlertRule rule);
    Task DeleteRuleAsync(string id);
}

public class FileBasedAlertRuleStore: IAlertRuleStore
{
    private readonly string _rulesDirectory;
    private readonly ILogger<FileBasedAlertRuleStore> _logger;

    public FileBasedAlertRuleStore(IConfiguration configuration, ILogger<FileBasedAlertRuleStore> logger)
    {
        _rulesDirectory = configuration["AlertRules:Directory"] ?? "./alert-rules";
        _logger = logger;

        if (!Directory.Exists(_rulesDirectory))
        {
            Directory.CreateDirectory(_rulesDirectory);
        }
    }

    public async Task<List<AlertRule>> GetAllRulesAsync()
    {
        var rules = new List<AlertRule>();
        var files = Directory.GetFiles(_rulesDirectory, "*.json");

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };

        foreach (var file in files)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var rule = JsonSerializer.Deserialize<AlertRule>(json, options);
                if (rule != null)
                {
                    rules.Add(rule);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading rule from {File}", file);
            }
        }

        return rules;
    }

    public async Task<AlertRule?> GetRuleAsync(string id)
    {
        var filePath = Path.Combine(_rulesDirectory, $"{id}.json");
        if (!File.Exists(filePath))
        {
            return null;
        }

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };

        var json = await File.ReadAllTextAsync(filePath);
        return JsonSerializer.Deserialize<AlertRule>(json, options);
    }

    public async Task SaveRuleAsync(AlertRule rule)
    {
        var filePath = Path.Combine(_rulesDirectory, $"{rule.Id}.json");
        var json = JsonSerializer.Serialize(rule, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(filePath, json);
    }

    public Task DeleteRuleAsync(string id)
    {
        var filePath = Path.Combine(_rulesDirectory, $"{id}.json");
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
        return Task.CompletedTask;
    }
}