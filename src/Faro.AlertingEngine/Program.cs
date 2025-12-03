using Faro.AlertingEngine;

var builder = Host.CreateApplicationBuilder(args);

// Add Alerting Engine service
builder.Services.AddHostedService<AlertingEngineWorker>();

var host = builder.Build();
host.Run();
