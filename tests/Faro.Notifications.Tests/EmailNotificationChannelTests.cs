using Faro.Notifications;
using Faro.Notifications.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Faro.Notifications.Tests;

public class EmailNotificationChannelTests
{
    private readonly Mock<ILogger<EmailNotificationChannel>> _mockLogger;
    private readonly EmailConfiguration _config;

    public EmailNotificationChannelTests()
    {
        _mockLogger = new Mock<ILogger<EmailNotificationChannel>>();
        _config = new EmailConfiguration
        {
            SmtpHost = "smtp.example.com",
            SmtpPort = 587,
            UseSsl = true,
            Username = "test@example.com",
            Password = "password",
            FromAddress = "alerts@example.com",
            FromName = "Faro Alerts",
            ToAddresses = new List<string> { "recipient@example.com" }
        };
    }

    [Fact]
    public void Constructor_WithValidConfig_CreatesInstance()
    {
        var options = Options.Create(_config);
        var channel = new EmailNotificationChannel(options, _mockLogger.Object);

        Assert.NotNull(channel);
        Assert.Equal("email", channel.Name);
    }

    [Fact]
    public void Name_ReturnsEmail()
    {
        var options = Options.Create(_config);
        var channel = new EmailNotificationChannel(options, _mockLogger.Object);

        Assert.Equal("email", channel.Name);
    }

    [Fact]
    public async Task SendAsync_WithInvalidSmtpHost_ThrowsException()
    {
        _config.SmtpHost = "invalid.smtp.server.that.does.not.exist";
        var options = Options.Create(_config);
        var channel = new EmailNotificationChannel(options, _mockLogger.Object);

        var message = new NotificationMessage
        {
            Title = "Test Alert",
            Body = "This is a test",
            Severity = NotificationSeverity.Warning
        };

        var exception = await Assert.ThrowsAnyAsync<Exception>(async () =>
            await channel.SendAsync(message, CancellationToken.None));

        Assert.NotNull(exception);
    }

    [Fact]
    public void EmailConfiguration_DefaultValues_AreCorrect()
    {
        var config = new EmailConfiguration();

        Assert.Equal(string.Empty, config.SmtpHost);
        Assert.Equal(587, config.SmtpPort);
        Assert.True(config.UseSsl);
        Assert.Equal(string.Empty, config.Username);
        Assert.Equal(string.Empty, config.Password);
        Assert.Equal(string.Empty, config.FromAddress);
        Assert.Equal("Faro Alerting", config.FromName);
        Assert.Empty(config.ToAddresses);
    }

    [Fact]
    public void EmailConfiguration_CanSetAllProperties()
    {
        var config = new EmailConfiguration
        {
            SmtpHost = "smtp.test.com",
            SmtpPort = 465,
            UseSsl = false,
            Username = "user",
            Password = "pass",
            FromAddress = "from@test.com",
            FromName = "Test Sender",
            ToAddresses = new List<string> { "to@test.com" }
        };

        Assert.Equal("smtp.test.com", config.SmtpHost);
        Assert.Equal(465, config.SmtpPort);
        Assert.False(config.UseSsl);
        Assert.Equal("user", config.Username);
        Assert.Equal("pass", config.Password);
        Assert.Equal("from@test.com", config.FromAddress);
        Assert.Equal("Test Sender", config.FromName);
        Assert.Single(config.ToAddresses);
        Assert.Equal("to@test.com", config.ToAddresses[0]);
    }
}
