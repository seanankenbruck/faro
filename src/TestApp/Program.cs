using Faro.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

// Configure services
builder.Services.AddMetricsClient(builder.Configuration);

var host = builder.Build();

// Get metrics client
var metricsClient = host.Services.GetRequiredService<MetricsClient>();
var logger = host.Services.GetRequiredService<ILogger<Program>>();

logger.LogInformation("Starting metrics test application...");

// Simulate sending metrics
var random = new Random();
var hostName = Environment.MachineName;

for (int i = 0; i < 100; i++)
{
    // CPU usage metric
    await metricsClient.RecordAsync(
        "cpu.usage",
        random.NextDouble() * 100,
        new Dictionary<string, string>
        {
            ["host"] = hostName,
            ["core"] = $"cpu{random.Next(0, 8)}"
        });

    // Memory usage metric
    await metricsClient.RecordAsync(
        "memory.usage",
        random.NextDouble() * 16384,
        new Dictionary<string, string>
        {
            ["host"] = hostName,
            ["type"] = "physical"
        });

    // API latency metric
    await metricsClient.RecordAsync(
        "api.latency",
        random.NextDouble() * 1000,
        new Dictionary<string, string>
        {
            ["host"] = hostName,
            ["endpoint"] = $"/api/endpoint{random.Next(1, 5)}",
            ["method"] = "GET"
        });

    if ((i + 1) % 10 == 0)
    {
        logger.LogInformation("Sent {Count} metrics", i + 1);
    }

    await Task.Delay(100);
}

// Flush remaining metrics
await metricsClient.FlushAsync();
logger.LogInformation("All metrics sent and flushed!");

metricsClient.Dispose();