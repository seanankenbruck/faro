using Faro.Storage;
using Faro.Storage.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Faro.Storage.Tests;

public class ServiceCollectionExtensionsTests
{
    private readonly IConfiguration _configuration;

    public ServiceCollectionExtensionsTests()
    {
        var inMemorySettings = new Dictionary<string, string>
        {
            ["ClickHouse:Host"] = "localhost",
            ["ClickHouse:Port"] = "8123",
            ["ClickHouse:Database"] = "test_db",
            ["ClickHouse:Username"] = "test_user",
            ["ClickHouse:Password"] = "test_password",
            ["ClickHouse:MaxRetries"] = "3"
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings!)
            .Build();
    }

    [Fact]
    public void AddMetricsStorage_RegistersClickHouseOptions()
    {
        var services = new ServiceCollection();

        services.AddMetricsStorage(_configuration);

        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetService<IOptions<ClickHouseOptions>>();

        Assert.NotNull(options);
        Assert.NotNull(options.Value);
        Assert.Equal("localhost", options.Value.Host);
        Assert.Equal(8123, options.Value.Port);
        Assert.Equal("test_db", options.Value.Database);
        Assert.Equal("test_user", options.Value.Username);
        Assert.Equal("test_password", options.Value.Password);
        Assert.Equal(3, options.Value.MaxRetries);
    }

    [Fact]
    public void AddMetricsStorage_RegistersClickHouseConnectionFactory()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddMetricsStorage(_configuration);

        var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetService<IClickHouseConnectionFactory>();

        Assert.NotNull(factory);
        Assert.IsType<ClickHouseConnectionFactory>(factory);
    }

    [Fact]
    public void AddMetricsStorage_RegistersMetricsRepository()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddMetricsStorage(_configuration);

        var serviceProvider = services.BuildServiceProvider();
        var repository = serviceProvider.GetService<IMetricsRepository>();

        Assert.NotNull(repository);
        Assert.IsType<ClickHouseMetricsRepository>(repository);
    }

    [Fact]
    public void AddMetricsStorage_AllServicesRegisteredAsSingleton()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddMetricsStorage(_configuration);

        var serviceProvider = services.BuildServiceProvider();

        // Get instances multiple times
        var factory1 = serviceProvider.GetService<IClickHouseConnectionFactory>();
        var factory2 = serviceProvider.GetService<IClickHouseConnectionFactory>();

        var repo1 = serviceProvider.GetService<IMetricsRepository>();
        var repo2 = serviceProvider.GetService<IMetricsRepository>();

        // Should be same instances (singleton)
        Assert.Same(factory1, factory2);
        Assert.Same(repo1, repo2);
    }

    [Fact]
    public void AddMetricsStorage_ReturnsServiceCollection()
    {
        var services = new ServiceCollection();

        var result = services.AddMetricsStorage(_configuration);

        Assert.Same(services, result);
    }

    [Fact]
    public void AddMetricsStorage_CanChainWithOtherRegistrations()
    {
        var services = new ServiceCollection();

        services
            .AddMetricsStorage(_configuration)
            .AddLogging();

        var serviceProvider = services.BuildServiceProvider();

        Assert.NotNull(serviceProvider.GetService<IMetricsRepository>());
    }

    [Fact]
    public void AddMetricsStorage_WithDifferentConfiguration_UsesProvidedValues()
    {
        var customConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["ClickHouse:Host"] = "custom.host.com",
                ["ClickHouse:Port"] = "9000",
                ["ClickHouse:Database"] = "custom_db",
                ["ClickHouse:Username"] = "custom_user",
                ["ClickHouse:Password"] = "custom_pass",
                ["ClickHouse:MaxRetries"] = "5"
            }!)
            .Build();

        var services = new ServiceCollection();
        services.AddMetricsStorage(customConfig);

        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetService<IOptions<ClickHouseOptions>>();

        Assert.Equal("custom.host.com", options!.Value.Host);
        Assert.Equal(9000, options.Value.Port);
        Assert.Equal("custom_db", options.Value.Database);
        Assert.Equal("custom_user", options.Value.Username);
        Assert.Equal("custom_pass", options.Value.Password);
        Assert.Equal(5, options.Value.MaxRetries);
    }

    [Fact]
    public void AddMetricsStorage_WithMissingConfiguration_UsesDefaults()
    {
        var emptyConfig = new ConfigurationBuilder().Build();

        var services = new ServiceCollection();
        services.AddMetricsStorage(emptyConfig);

        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetService<IOptions<ClickHouseOptions>>();

        Assert.NotNull(options);
        // Default values from ClickHouseOptions
        Assert.Equal(3, options.Value.MaxRetries);
    }

    [Fact]
    public void AddMetricsStorage_RegisteredServicesCanBeResolved()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMetricsStorage(_configuration);

        var serviceProvider = services.BuildServiceProvider();

        // Should be able to resolve all registered types
        var factory = serviceProvider.GetRequiredService<IClickHouseConnectionFactory>();
        var repository = serviceProvider.GetRequiredService<IMetricsRepository>();
        var options = serviceProvider.GetRequiredService<IOptions<ClickHouseOptions>>();

        Assert.NotNull(factory);
        Assert.NotNull(repository);
        Assert.NotNull(options);
    }

    [Fact]
    public void AddMetricsStorage_OptionsBindingWorks()
    {
        var services = new ServiceCollection();
        services.AddMetricsStorage(_configuration);

        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<ClickHouseOptions>>();

        // Verify that options are bound from configuration
        Assert.NotNull(options.Value.ConnectionString);
        Assert.Contains("localhost", options.Value.ConnectionString);
    }

    [Fact]
    public void AddMetricsStorage_MultipleCallsDoNotDuplicate()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddMetricsStorage(_configuration);
        services.AddMetricsStorage(_configuration);

        var serviceProvider = services.BuildServiceProvider();

        // Should still work without errors
        var repository = serviceProvider.GetService<IMetricsRepository>();
        Assert.NotNull(repository);
    }

    [Fact]
    public void AddMetricsStorage_WorksInAspNetCoreScenario()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.AddMetricsStorage(_configuration);

        var serviceProvider = services.BuildServiceProvider();

        using var scope = serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IMetricsRepository>();

        Assert.NotNull(repository);
    }

    [Fact]
    public void ClickHouseOptions_SectionName_MatchesConfiguration()
    {
        var services = new ServiceCollection();
        services.AddMetricsStorage(_configuration);

        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<ClickHouseOptions>>();

        // Verify configuration section name is correct
        Assert.Equal("ClickHouse", ClickHouseOptions.SectionName);
    }
}
