using Faro.Consumer.Services;
using Faro.Storage;

var builder = Host.CreateApplicationBuilder(args);

// Add storage layer
builder.Services.AddMetricsStorage(builder.Configuration);

// Add Kafka consumer service
builder.Services.AddHostedService<MetricsConsumerService>();

var host = builder.Build();
host.Run();
