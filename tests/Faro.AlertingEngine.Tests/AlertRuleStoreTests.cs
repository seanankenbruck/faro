using Faro.AlertingEngine.Models;
using Faro.AlertingEngine.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace Faro.AlertingEngine.Tests;

public class AlertRuleStoreTests : IDisposable
{
    private readonly Mock<ILogger<FileBasedAlertRuleStore>> _mockLogger;
    private readonly string _testDirectory;
    private readonly IConfiguration _configuration;

    public AlertRuleStoreTests()
    {
        _mockLogger = new Mock<ILogger<FileBasedAlertRuleStore>>();
        _testDirectory = Path.Combine(Path.GetTempPath(), $"faro-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);

        var inMemorySettings = new Dictionary<string, string>
        {
            ["AlertRules:Directory"] = _testDirectory
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings!)
            .Build();
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    [Fact]
    public void Constructor_DirectoryNotExist_CreatesDirectory()
    {
        var newDirectory = Path.Combine(_testDirectory, "new-dir");

        var configWithNewDir = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["AlertRules:Directory"] = newDirectory
            }!)
            .Build();

        var store = new FileBasedAlertRuleStore(configWithNewDir, _mockLogger.Object);

        Assert.True(Directory.Exists(newDirectory));

        Directory.Delete(newDirectory);
    }

    [Fact]
    public void Constructor_NoConfiguration_UsesDefaultDirectory()
    {
        var configWithoutDirectory = new ConfigurationBuilder().Build();

        var store = new FileBasedAlertRuleStore(configWithoutDirectory, _mockLogger.Object);

        Assert.True(Directory.Exists("./alert-rules"));

        if (Directory.Exists("./alert-rules"))
        {
            Directory.Delete("./alert-rules", recursive: true);
        }
    }

    [Fact]
    public async Task SaveRuleAsync_CreatesJsonFile()
    {
        var store = CreateStoreForTesting();
        var rule = CreateValidRule("test-rule");

        await store.SaveRuleAsync(rule);

        var filePath = Path.Combine(_testDirectory, "test-rule.json");
        Assert.True(File.Exists(filePath));

        var json = await File.ReadAllTextAsync(filePath);
        Assert.Contains("test-rule", json);
        Assert.Contains("Test Rule", json);
    }

    [Fact]
    public async Task GetRuleAsync_FileExists_ReturnsRule()
    {
        var store = CreateStoreForTesting();
        var rule = CreateValidRule("test-rule");
        await store.SaveRuleAsync(rule);

        var retrieved = await store.GetRuleAsync("test-rule");

        Assert.NotNull(retrieved);
        Assert.Equal("test-rule", retrieved.Id);
        Assert.Equal("Test Rule", retrieved.Name);
        Assert.Equal(ComparisonOperator.GreaterThan, retrieved.Condition.Operator);
        Assert.Equal(100.0, retrieved.Condition.Threshold);
    }

    [Fact]
    public async Task GetRuleAsync_FileNotExists_ReturnsNull()
    {
        var store = CreateStoreForTesting();

        var result = await store.GetRuleAsync("non-existent");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAllRulesAsync_NoRules_ReturnsEmptyList()
    {
        var store = CreateStoreForTesting();

        var rules = await store.GetAllRulesAsync();

        Assert.Empty(rules);
    }

    [Fact]
    public async Task GetAllRulesAsync_MultipleRules_ReturnsAll()
    {
        var store = CreateStoreForTesting();

        await store.SaveRuleAsync(CreateValidRule("rule-1"));
        await store.SaveRuleAsync(CreateValidRule("rule-2"));
        await store.SaveRuleAsync(CreateValidRule("rule-3"));

        var rules = await store.GetAllRulesAsync();

        Assert.Equal(3, rules.Count);
        Assert.Contains(rules, r => r.Id == "rule-1");
        Assert.Contains(rules, r => r.Id == "rule-2");
        Assert.Contains(rules, r => r.Id == "rule-3");
    }

    [Fact]
    public async Task GetAllRulesAsync_InvalidFile_SkipsFile()
    {
        var store = CreateStoreForTesting();

        await store.SaveRuleAsync(CreateValidRule("valid"));

        var invalidFile = Path.Combine(_testDirectory, "invalid.json");
        await File.WriteAllTextAsync(invalidFile, "{ invalid json }");

        var rules = await store.GetAllRulesAsync();

        Assert.Single(rules);
        Assert.Equal("valid", rules[0].Id);
    }

    [Fact]
    public async Task DeleteRuleAsync_FileExists_RemovesFile()
    {
        var store = CreateStoreForTesting();
        await store.SaveRuleAsync(CreateValidRule("to-delete"));

        var filePath = Path.Combine(_testDirectory, "to-delete.json");
        Assert.True(File.Exists(filePath));

        await store.DeleteRuleAsync("to-delete");

        Assert.False(File.Exists(filePath));
    }

    [Fact]
    public async Task DeleteRuleAsync_FileNotExists_DoesNotThrow()
    {
        var store = CreateStoreForTesting();

        await store.DeleteRuleAsync("non-existent");

        // Should not throw
    }

    [Fact]
    public async Task SaveRuleAsync_ExistingRule_Overwrites()
    {
        var store = CreateStoreForTesting();

        var originalRule = CreateValidRule("test");
        await store.SaveRuleAsync(originalRule);

        var updatedRule = CreateValidRule("test");
        updatedRule.Name = "Updated Rule";
        await store.SaveRuleAsync(updatedRule);

        var retrieved = await store.GetRuleAsync("test");

        Assert.Equal("Updated Rule", retrieved!.Name);
    }

    [Fact]
    public async Task SaveRuleAsync_FormatsJsonWithIndentation()
    {
        var store = CreateStoreForTesting();
        var rule = CreateValidRule("test");

        await store.SaveRuleAsync(rule);

        var filePath = Path.Combine(_testDirectory, "test.json");
        var json = await File.ReadAllTextAsync(filePath);

        Assert.Contains("\n", json);
    }

    private FileBasedAlertRuleStore CreateStoreForTesting()
    {
        return new FileBasedAlertRuleStore(_configuration, _mockLogger.Object);
    }

    private AlertRule CreateValidRule(string id)
    {
        return new AlertRule
        {
            Id = id,
            Name = "Test Rule",
            Query = "SELECT 1",
            Condition = new AlertCondition
            {
                Operator = ComparisonOperator.GreaterThan,
                Threshold = 100.0
            }
        };
    }
}
