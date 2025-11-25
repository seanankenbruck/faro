using Faro.Shared.Models;
using Faro.Shared.Validation;

namespace Faro.Collector.Tests;

public class MetricPointValidatorTests
{
    private readonly MetricPointValidator _validator;

    public MetricPointValidatorTests()
    {
        _validator = new MetricPointValidator();
    }

    [Fact]
    public async Task ValidMetric_PassesValidation()
    {
        var metric = new MetricPoint
        {
            MetricName = "test.metric",
            Value = 42.0,
            Timestamp = DateTime.UtcNow,
            Tags = new Dictionary<string, string>
            {
                ["host"] = "test-host"
            }
        };

        var result = await _validator.ValidateAsync(metric);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task MetricName_Required()
    {
        var metric = new MetricPoint
        {
            MetricName = null!,
            Value = 42.0,
            Timestamp = DateTime.UtcNow,
            Tags = new Dictionary<string, string>()
        };

        var result = await _validator.ValidateAsync(metric);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "MetricName");
    }

    [Fact]
    public async Task MetricName_EmptyString_FailsValidation()
    {
        var metric = new MetricPoint
        {
            MetricName = "",
            Value = 42.0,
            Timestamp = DateTime.UtcNow,
            Tags = new Dictionary<string, string>()
        };

        var result = await _validator.ValidateAsync(metric);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "MetricName");
    }

    [Fact]
    public async Task MetricName_TooLong_FailsValidation()
    {
        var metric = new MetricPoint
        {
            MetricName = new string('a', 201),
            Value = 42.0,
            Timestamp = DateTime.UtcNow,
            Tags = new Dictionary<string, string>()
        };

        var result = await _validator.ValidateAsync(metric);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "MetricName");
    }

    [Fact]
    public async Task MetricName_MustStartWithLetter()
    {
        var metric = new MetricPoint
        {
            MetricName = "1metric",
            Value = 42.0,
            Timestamp = DateTime.UtcNow,
            Tags = new Dictionary<string, string>()
        };

        var result = await _validator.ValidateAsync(metric);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "MetricName");
    }

    [Theory]
    [InlineData("metric.name")]
    [InlineData("metric_name")]
    [InlineData("metric-name")]
    [InlineData("metric123")]
    [InlineData("m")]
    [InlineData("metric.name.with.dots")]
    [InlineData("metric_name_with_underscores")]
    [InlineData("metric-name-with-dashes")]
    public async Task MetricName_ValidPatterns_PassValidation(string metricName)
    {
        var metric = new MetricPoint
        {
            MetricName = metricName,
            Value = 42.0,
            Timestamp = DateTime.UtcNow,
            Tags = new Dictionary<string, string>()
        };

        var result = await _validator.ValidateAsync(metric);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("metric name")]
    [InlineData("metric@name")]
    [InlineData("metric$name")]
    [InlineData("metric#name")]
    [InlineData(".metric")]
    [InlineData("_metric")]
    [InlineData("-metric")]
    public async Task MetricName_InvalidPatterns_FailValidation(string metricName)
    {
        var metric = new MetricPoint
        {
            MetricName = metricName,
            Value = 42.0,
            Timestamp = DateTime.UtcNow,
            Tags = new Dictionary<string, string>()
        };

        var result = await _validator.ValidateAsync(metric);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "MetricName");
    }

    [Fact]
    public async Task Timestamp_Required()
    {
        var metric = new MetricPoint
        {
            MetricName = "test.metric",
            Value = 42.0,
            Timestamp = default,
            Tags = new Dictionary<string, string>()
        };

        var result = await _validator.ValidateAsync(metric);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Timestamp");
    }

    [Fact]
    public async Task Timestamp_WithinLastHour_PassesValidation()
    {
        var metric = new MetricPoint
        {
            MetricName = "test.metric",
            Value = 42.0,
            Timestamp = DateTime.UtcNow.AddMinutes(-30),
            Tags = new Dictionary<string, string>()
        };

        var result = await _validator.ValidateAsync(metric);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Timestamp_TooOld_FailsValidation()
    {
        var metric = new MetricPoint
        {
            MetricName = "test.metric",
            Value = 42.0,
            Timestamp = DateTime.UtcNow.AddHours(-2),
            Tags = new Dictionary<string, string>()
        };

        var result = await _validator.ValidateAsync(metric);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Timestamp");
    }

    [Fact]
    public async Task Timestamp_InFuture_FailsValidation()
    {
        var metric = new MetricPoint
        {
            MetricName = "test.metric",
            Value = 42.0,
            Timestamp = DateTime.UtcNow.AddMinutes(5),
            Tags = new Dictionary<string, string>()
        };

        var result = await _validator.ValidateAsync(metric);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Timestamp");
    }

    [Fact]
    public async Task Value_NaN_FailsValidation()
    {
        var metric = new MetricPoint
        {
            MetricName = "test.metric",
            Value = double.NaN,
            Timestamp = DateTime.UtcNow,
            Tags = new Dictionary<string, string>()
        };

        var result = await _validator.ValidateAsync(metric);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Value");
    }

    [Fact]
    public async Task Value_PositiveInfinity_FailsValidation()
    {
        var metric = new MetricPoint
        {
            MetricName = "test.metric",
            Value = double.PositiveInfinity,
            Timestamp = DateTime.UtcNow,
            Tags = new Dictionary<string, string>()
        };

        var result = await _validator.ValidateAsync(metric);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Value");
    }

    [Fact]
    public async Task Value_NegativeInfinity_FailsValidation()
    {
        var metric = new MetricPoint
        {
            MetricName = "test.metric",
            Value = double.NegativeInfinity,
            Timestamp = DateTime.UtcNow,
            Tags = new Dictionary<string, string>()
        };

        var result = await _validator.ValidateAsync(metric);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Value");
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(42.5)]
    [InlineData(-100.75)]
    [InlineData(999999.99)]
    [InlineData(double.MaxValue)]
    [InlineData(double.MinValue)]
    public async Task Value_ValidNumbers_PassValidation(double value)
    {
        var metric = new MetricPoint
        {
            MetricName = "test.metric",
            Value = value,
            Timestamp = DateTime.UtcNow,
            Tags = new Dictionary<string, string>()
        };

        var result = await _validator.ValidateAsync(metric);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Tags_NotNull()
    {
        var metric = new MetricPoint
        {
            MetricName = "test.metric",
            Value = 42.0,
            Timestamp = DateTime.UtcNow,
            Tags = null!
        };

        var result = await _validator.ValidateAsync(metric);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Tags");
    }

    [Fact]
    public async Task Tags_EmptyDictionary_PassesValidation()
    {
        var metric = new MetricPoint
        {
            MetricName = "test.metric",
            Value = 42.0,
            Timestamp = DateTime.UtcNow,
            Tags = new Dictionary<string, string>()
        };

        var result = await _validator.ValidateAsync(metric);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Tags_UpTo20Tags_PassesValidation()
    {
        var tags = Enumerable.Range(1, 20)
            .ToDictionary(i => $"tag{i}", i => $"value{i}");

        var metric = new MetricPoint
        {
            MetricName = "test.metric",
            Value = 42.0,
            Timestamp = DateTime.UtcNow,
            Tags = tags
        };

        var result = await _validator.ValidateAsync(metric);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Tags_MoreThan20Tags_FailsValidation()
    {
        var tags = Enumerable.Range(1, 21)
            .ToDictionary(i => $"tag{i}", i => $"value{i}");

        var metric = new MetricPoint
        {
            MetricName = "test.metric",
            Value = 42.0,
            Timestamp = DateTime.UtcNow,
            Tags = tags
        };

        var result = await _validator.ValidateAsync(metric);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Tags");
    }

    [Fact]
    public async Task MultipleValidationErrors_AllReported()
    {
        var metric = new MetricPoint
        {
            MetricName = "123invalid",
            Value = double.NaN,
            Timestamp = DateTime.UtcNow.AddHours(-5),
            Tags = null!
        };

        var result = await _validator.ValidateAsync(metric);

        Assert.False(result.IsValid);
        Assert.True(result.Errors.Count >= 3);
        Assert.Contains(result.Errors, e => e.PropertyName == "MetricName");
        Assert.Contains(result.Errors, e => e.PropertyName == "Value");
        Assert.Contains(result.Errors, e => e.PropertyName == "Timestamp");
    }
}
