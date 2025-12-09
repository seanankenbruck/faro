using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace Faro.Notifications.Channels;

public class EmailNotificationChannel : INotificationChannel
{
    public string Name => "email";

    private readonly ILogger<EmailNotificationChannel> _logger;
    private readonly EmailConfiguration _config;

    public EmailNotificationChannel(IOptions<EmailConfiguration> options, ILogger<EmailNotificationChannel> logger)
    {
        _logger = logger;
        _config = options.Value;
    }

    public async Task SendAsync(NotificationMessage message, CancellationToken cancellationToken = default)
    {
        try
        {
            var emailMessage = new MimeMessage();
            emailMessage.From.Add(new MailboxAddress(_config.FromName, _config.FromAddress));

            foreach (var to in _config.ToAddresses)
            {
                emailMessage.To.Add(MailboxAddress.Parse(to));
            }

            emailMessage.Subject = $"[{message.Severity}] {message.Title}";
            emailMessage.Body = new TextPart("plain") { Text = message.Body };

            using var client = new SmtpClient();
            // Use StartTls for port 587, SslOnConnect for port 465
            var secureSocketOptions = _config.SmtpPort == 465
                ? SecureSocketOptions.SslOnConnect
                : SecureSocketOptions.StartTls;

            await client.ConnectAsync(_config.SmtpHost, _config.SmtpPort, secureSocketOptions,
            cancellationToken);

            if (!string.IsNullOrEmpty(_config.Username))
            {
                await client.AuthenticateAsync(_config.Username, _config.Password,
                cancellationToken);
            }

            await client.SendAsync(emailMessage, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            _logger.LogInformation("Email notification sent: {Title}", message.Title);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email notification");
            throw;
        }
    }
}

public class EmailConfiguration
{
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "Faro Alerting";
    public List<string> ToAddresses { get; set; } = new();
}