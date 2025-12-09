using Faro.AlertingEngine;
using Faro.AlertingEngine.Services;
using Faro.Notifications;
using Faro.Notifications.Channels;

var builder = Host.CreateApplicationBuilder(args);

// Register alerting services
builder.Services.AddSingleton<IAlertRuleStore, FileBasedAlertRuleStore>();
builder.Services.AddSingleton<IAlertStateManager, InMemoryAlertStateManager>();
builder.Services.AddSingleton<IAlertEvaluator, AlertEvaluator>();

// Register notification channels
builder.Services.AddHttpClient();

// Configure and register email notification channel
builder.Services.Configure<EmailConfiguration>(
    builder.Configuration.GetSection("Notifications:Channels:email"));
builder.Services.AddSingleton<INotificationChannel, EmailNotificationChannel>();

// Configure and register webhook notification channel
var webhookUrl = builder.Configuration["Notifications:Channels:webhook:Url"];
if (!string.IsNullOrEmpty(webhookUrl))
{
    builder.Services.AddSingleton<INotificationChannel>(sp =>
        new WebhookNotificationChannel(
            sp.GetRequiredService<IHttpClientFactory>(),
            sp.GetRequiredService<ILogger<WebhookNotificationChannel>>(),
            webhookUrl));
}

// Register Alerting Engine worker
builder.Services.AddHostedService<AlertingEngineWorker>();

var host = builder.Build();
host.Run();
