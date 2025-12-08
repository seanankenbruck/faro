namespace Faro.Notifications;

public interface INotificationChannel
{
    string Name { get; }
    Task SendAsync(NotificationMessage message, CancellationToken cancellationToken = default);
}

public class NotificationMessage
{
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public NotificationSeverity Severity { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = [];
}

public enum NotificationSeverity
{
    Info,
    Warning,
    Critical
}