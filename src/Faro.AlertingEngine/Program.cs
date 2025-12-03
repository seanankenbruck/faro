using Faro.AlertingEngine;
using Faro.AlertingEngine.Services;

var builder = Host.CreateApplicationBuilder(args);

// Register alerting services
builder.Services.AddSingleton<IAlertRuleStore, FileBasedAlertRuleStore>();
builder.Services.AddSingleton<IAlertStateManager, InMemoryAlertStateManager>();
builder.Services.AddSingleton<IAlertEvaluator, AlertEvaluator>();

// Register Alerting Engine worker
builder.Services.AddHostedService<AlertingEngineWorker>();

var host = builder.Build();
host.Run();
