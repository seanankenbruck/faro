using System.Net;
using Faro.Notifications;
using Faro.Notifications.Channels;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;

namespace Faro.Notifications.Tests;

public class WebhookNotificationChannelTests
{
    private readonly Mock<ILogger<WebhookNotificationChannel>> _mockLogger;
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly string _testWebhookUrl = "https://example.com/webhook";

    public WebhookNotificationChannelTests()
    {
        _mockLogger = new Mock<ILogger<WebhookNotificationChannel>>();
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
    }

    [Fact]
    public void Constructor_WithValidUrl_CreatesInstance()
    {
        var httpClient = new HttpClient(_mockHttpMessageHandler.Object);
        _mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var channel = new WebhookNotificationChannel(
            _mockHttpClientFactory.Object,
            _mockLogger.Object,
            _testWebhookUrl);

        Assert.NotNull(channel);
        Assert.Equal("webhook", channel.Name);
    }

    [Fact]
    public void Constructor_WithNullUrl_ThrowsArgumentNullException()
    {
        var httpClient = new HttpClient(_mockHttpMessageHandler.Object);
        _mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        Assert.Throws<ArgumentNullException>(() =>
            new WebhookNotificationChannel(
                _mockHttpClientFactory.Object,
                _mockLogger.Object,
                null!));
    }

    [Fact]
    public void Name_ReturnsWebhook()
    {
        var httpClient = new HttpClient(_mockHttpMessageHandler.Object);
        _mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var channel = new WebhookNotificationChannel(
            _mockHttpClientFactory.Object,
            _mockLogger.Object,
            _testWebhookUrl);

        Assert.Equal("webhook", channel.Name);
    }

    [Fact]
    public async Task SendAsync_WithSuccessfulResponse_CompletesSuccessfully()
    {
        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"success\":true}")
            });

        var httpClient = new HttpClient(_mockHttpMessageHandler.Object);
        _mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var channel = new WebhookNotificationChannel(
            _mockHttpClientFactory.Object,
            _mockLogger.Object,
            _testWebhookUrl);

        var message = new NotificationMessage
        {
            Title = "Test Alert",
            Body = "This is a test",
            Severity = NotificationSeverity.Critical,
            Metadata = new Dictionary<string, string>
            {
                ["rule_id"] = "test-rule",
                ["state"] = "Firing"
            }
        };

        await channel.SendAsync(message, CancellationToken.None);

        _mockHttpMessageHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Post &&
                req.RequestUri!.ToString() == _testWebhookUrl),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_WithFailedResponse_ThrowsException()
    {
        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError,
                Content = new StringContent("Server error")
            });

        var httpClient = new HttpClient(_mockHttpMessageHandler.Object);
        _mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var channel = new WebhookNotificationChannel(
            _mockHttpClientFactory.Object,
            _mockLogger.Object,
            _testWebhookUrl);

        var message = new NotificationMessage
        {
            Title = "Test Alert",
            Body = "This is a test",
            Severity = NotificationSeverity.Warning
        };

        await Assert.ThrowsAsync<HttpRequestException>(async () =>
            await channel.SendAsync(message, CancellationToken.None));
    }

    [Fact]
    public async Task SendAsync_WithNetworkError_ThrowsException()
    {
        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        var httpClient = new HttpClient(_mockHttpMessageHandler.Object);
        _mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var channel = new WebhookNotificationChannel(
            _mockHttpClientFactory.Object,
            _mockLogger.Object,
            _testWebhookUrl);

        var message = new NotificationMessage
        {
            Title = "Test Alert",
            Body = "This is a test",
            Severity = NotificationSeverity.Info
        };

        await Assert.ThrowsAsync<HttpRequestException>(async () =>
            await channel.SendAsync(message, CancellationToken.None));
    }
}
