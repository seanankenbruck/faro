using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Faro.Notifications.Channels;

public class WebhookNotificationChannel: INotificationChannel
{
    public string Name => "webhook";
    private readonly ILogger<WebhookNotificationChannel> _logger;
    private readonly HttpClient _httpClient;
    private readonly string _webhookUrl;

    public WebhookNotificationChannel(
        IHttpClientFactory httpClientFactory,
        ILogger<WebhookNotificationChannel> logger,
        string webhookUrl)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
        _webhookUrl = webhookUrl ?? throw new ArgumentNullException(nameof(webhookUrl));
    }

    public async Task SendAsync(NotificationMessage message, CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = new
            {
                title = message.Title,
                body = message.Body,
                severity = message.Severity.ToString(),
                timestamp = DateTime.UtcNow,
                metadata = message.Metadata
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(_webhookUrl, content, cancellationToken);
            response.EnsureSuccessStatusCode();

            _logger.LogInformation("Webhook notification sent: {Title}", message.Title);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send webhook notification");
            throw;
        }
    }
}