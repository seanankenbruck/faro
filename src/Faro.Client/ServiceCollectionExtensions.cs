using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Faro.Client;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMetricsClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<MetricsClientOptions>(
            configuration.GetSection(MetricsClientOptions.SectionName));

        services.AddHttpClient<MetricsClient>();

        return services;
    }
}