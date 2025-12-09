using Faro.Notifications;

namespace Faro.Notifications.Tests;

public class NotificationMessageTests
{
    [Fact]
    public void Constructor_CreatesInstanceWithDefaults()
    {
        var message = new NotificationMessage();

        Assert.Equal(string.Empty, message.Title);
        Assert.Equal(string.Empty, message.Body);
        Assert.Equal(NotificationSeverity.Info, message.Severity);
        Assert.NotNull(message.Metadata);
        Assert.Empty(message.Metadata);
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        var message = new NotificationMessage
        {
            Title = "Test Title",
            Body = "Test Body",
            Severity = NotificationSeverity.Critical,
            Metadata = new Dictionary<string, string>
            {
                ["key1"] = "value1",
                ["key2"] = "value2"
            }
        };

        Assert.Equal("Test Title", message.Title);
        Assert.Equal("Test Body", message.Body);
        Assert.Equal(NotificationSeverity.Critical, message.Severity);
        Assert.Equal(2, message.Metadata.Count);
        Assert.Equal("value1", message.Metadata["key1"]);
        Assert.Equal("value2", message.Metadata["key2"]);
    }

    [Fact]
    public void Metadata_CanBeModified()
    {
        var message = new NotificationMessage();

        message.Metadata["test"] = "value";
        message.Metadata.Add("another", "data");

        Assert.Equal(2, message.Metadata.Count);
        Assert.True(message.Metadata.ContainsKey("test"));
        Assert.True(message.Metadata.ContainsKey("another"));
    }

    [Theory]
    [InlineData(NotificationSeverity.Info)]
    [InlineData(NotificationSeverity.Warning)]
    [InlineData(NotificationSeverity.Critical)]
    public void Severity_CanBeSetToAnyValue(NotificationSeverity severity)
    {
        var message = new NotificationMessage
        {
            Severity = severity
        };

        Assert.Equal(severity, message.Severity);
    }
}

public class NotificationSeverityTests
{
    [Fact]
    public void NotificationSeverity_HasExpectedValues()
    {
        Assert.Equal(0, (int)NotificationSeverity.Info);
        Assert.Equal(1, (int)NotificationSeverity.Warning);
        Assert.Equal(2, (int)NotificationSeverity.Critical);
    }

    [Fact]
    public void NotificationSeverity_CanBeCompared()
    {
        Assert.True(NotificationSeverity.Info < NotificationSeverity.Warning);
        Assert.True(NotificationSeverity.Warning < NotificationSeverity.Critical);
        Assert.True(NotificationSeverity.Info < NotificationSeverity.Critical);
    }

    [Theory]
    [InlineData(NotificationSeverity.Info, "Info")]
    [InlineData(NotificationSeverity.Warning, "Warning")]
    [InlineData(NotificationSeverity.Critical, "Critical")]
    public void NotificationSeverity_ToString_ReturnsName(NotificationSeverity severity, string expected)
    {
        Assert.Equal(expected, severity.ToString());
    }
}
