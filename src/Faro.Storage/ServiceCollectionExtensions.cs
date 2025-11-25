using Faro.Storage.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Faro.Storage;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds metrics storage services to the DI container
    /// </summary>
    public static IServiceCollection AddMetricsStorage(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure ClickHouse options
        services.Configure<ClickHouseOptions>(
            configuration.GetSection(ClickHouseOptions.SectionName));

        // Register ClickHouse connection factory
        services.AddSingleton<IClickHouseConnectionFactory, ClickHouseConnectionFactory>();

        // Register ClickHouse metrics repository
        services.AddSingleton<IMetricsRepository, ClickHouseMetricsRepository>();

        return services;
    }

}