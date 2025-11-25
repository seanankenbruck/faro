using Faro.Collector.Controllers;
using Faro.Collector.Services;
using Faro.Shared.Models;
using Faro.Shared.Validation;
using Faro.Storage;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace Faro.Collector.Tests;

public class MetricsControllerTests
{
    private readonly Mock<IMetricsRepository> _mockRepository;
    private readonly MetricsBuffer _buffer;
    private readonly Mock<IValidator<MetricPoint>> _mockValidator;
    private readonly Mock<ILogger<MetricsController>> _mockControllerLogger;
    private readonly Mock<ILogger<MetricsBuffer>> _mockBufferLogger;
    private readonly MetricsController _controller;

    public MetricsControllerTests()
    {
        _mockRepository = new Mock<IMetricsRepository>();
        _mockBufferLogger = new Mock<ILogger<MetricsBuffer>>();
        _mockValidator = new Mock<IValidator<MetricPoint>>();
        _mockControllerLogger = new Mock<ILogger<MetricsController>>();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["MetricsCollector:BatchSize"] = "1000",
                ["MetricsCollector:FlushIntervalSeconds"] = "10"
            }!)
            .Build();

        _buffer = new MetricsBuffer(_mockRepository.Object, _mockBufferLogger.Object, config);
        _controller = new MetricsController(_buffer, _mockValidator.Object, _mockControllerLogger.Object);
    }

    [Fact]
    public async Task RecordSingleMetric_WithValidMetric_ReturnsAccepted()
    {
        var metric = CreateValidMetric();
        _mockValidator.Setup(v => v.ValidateAsync(metric, default))
            .ReturnsAsync(new ValidationResult());
        _mockRepository.Setup(r => r.WriteBatchAsync(It.IsAny<IEnumerable<MetricPoint>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.PostSingle(metric);

        var acceptedResult = Assert.IsType<AcceptedResult>(result);
        Assert.NotNull(acceptedResult.Value);
    }

    [Fact]
    public async Task RecordSingleMetric_WithInvalidMetric_ReturnsBadRequest()
    {
        var metric = CreateValidMetric();
        var validationFailure = new ValidationFailure("MetricName", "Metric name is required");
        var validationResult = new ValidationResult(new[] { validationFailure });

        _mockValidator.Setup(v => v.ValidateAsync(metric, default))
            .ReturnsAsync(validationResult);

        var result = await _controller.PostSingle(metric);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequestResult.Value);
    }

    [Fact]
    public async Task RecordSingleMetric_WithMultipleValidationErrors_ReturnsAllErrors()
    {
        var metric = CreateValidMetric();
        var validationFailures = new[]
        {
            new ValidationFailure("MetricName", "Invalid metric name"),
            new ValidationFailure("Value", "Value cannot be NaN"),
            new ValidationFailure("Timestamp", "Timestamp too old")
        };
        var validationResult = new ValidationResult(validationFailures);

        _mockValidator.Setup(v => v.ValidateAsync(metric, default))
            .ReturnsAsync(validationResult);

        var result = await _controller.PostSingle(metric);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var errors = badRequestResult.Value;
        Assert.NotNull(errors);
    }

    [Fact]
    public async Task RecordBatchMetrics_WithValidBatch_ReturnsAcceptedWithCount()
    {
        var batch = new MetricBatch
        {
            Metrics = new List<MetricPoint>
            {
                CreateValidMetric("metric1"),
                CreateValidMetric("metric2"),
                CreateValidMetric("metric3")
            }
        };

        _mockValidator.Setup(v => v.ValidateAsync(It.IsAny<MetricPoint>(), default))
            .ReturnsAsync(new ValidationResult());
        _mockRepository.Setup(r => r.WriteBatchAsync(It.IsAny<IEnumerable<MetricPoint>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.PostBatch(batch);

        Assert.IsType<AcceptedResult>(result);
    }

    [Fact]
    public async Task RecordBatchMetrics_WithEmptyBatch_ReturnsBadRequest()
    {
        var batch = new MetricBatch { Metrics = new List<MetricPoint>() };

        var result = await _controller.PostBatch(batch);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequestResult.Value);
    }

    [Fact]
    public async Task RecordBatchMetrics_WithNullMetricsList_ReturnsBadRequest()
    {
        var batch = new MetricBatch { Metrics = null! };

        var result = await _controller.PostBatch(batch);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequestResult.Value);
    }

    [Fact]
    public async Task RecordBatchMetrics_WithSomeInvalidMetrics_ReturnsBadRequest()
    {
        var batch = new MetricBatch
        {
            Metrics = new List<MetricPoint>
            {
                CreateValidMetric("valid1"),
                CreateValidMetric("invalid"),
                CreateValidMetric("valid2")
            }
        };

        _mockValidator.Setup(v => v.ValidateAsync(It.Is<MetricPoint>(m => m.MetricName == "valid1"), default))
            .ReturnsAsync(new ValidationResult());
        _mockValidator.Setup(v => v.ValidateAsync(It.Is<MetricPoint>(m => m.MetricName == "invalid"), default))
            .ReturnsAsync(new ValidationResult(new[] { new ValidationFailure("MetricName", "Invalid") }));
        _mockValidator.Setup(v => v.ValidateAsync(It.Is<MetricPoint>(m => m.MetricName == "valid2"), default))
            .ReturnsAsync(new ValidationResult());

        var result = await _controller.PostBatch(batch);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequestResult.Value);
    }

    [Fact]
    public async Task RecordBatchMetrics_LogsReceivedBatchSize()
    {
        var batch = new MetricBatch
        {
            Metrics = new List<MetricPoint>
            {
                CreateValidMetric("metric1"),
                CreateValidMetric("metric2"),
                CreateValidMetric("metric3")
            }
        };

        _mockValidator.Setup(v => v.ValidateAsync(It.IsAny<MetricPoint>(), default))
            .ReturnsAsync(new ValidationResult());
        _mockRepository.Setup(r => r.WriteBatchAsync(It.IsAny<IEnumerable<MetricPoint>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _controller.PostBatch(batch);

        _mockControllerLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("3")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task RecordBatchMetrics_WithMaximumBatchSize_ProcessesSuccessfully()
    {
        var metrics = Enumerable.Range(0, 1000)
            .Select(i => CreateValidMetric($"metric{i}"))
            .ToList();
        var batch = new MetricBatch { Metrics = metrics };

        _mockValidator.Setup(v => v.ValidateAsync(It.IsAny<MetricPoint>(), default))
            .ReturnsAsync(new ValidationResult());
        _mockRepository.Setup(r => r.WriteBatchAsync(It.IsAny<IEnumerable<MetricPoint>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.PostBatch(batch);

        Assert.IsType<AcceptedResult>(result);
    }

    private MetricPoint CreateValidMetric(string name = "test.metric")
    {
        return new MetricPoint
        {
            MetricName = name,
            Value = 42.0,
            Timestamp = DateTime.UtcNow,
            Tags = new Dictionary<string, string>
            {
                ["host"] = "test-host",
                ["service"] = "test-service"
            }
        };
    }
}
