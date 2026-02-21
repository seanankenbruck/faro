using Faro.Collector.Controllers;
using Faro.Storage;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Faro.Collector.Tests;

public class HealthControllerTests
{
    private readonly Mock<IMetricsRepository> _mockRepository;
    private readonly HealthController _controller;

    public HealthControllerTests()
    {
        _mockRepository = new Mock<IMetricsRepository>();
        _controller = new HealthController(_mockRepository.Object);
    }

    [Fact]
    public async Task Get_WhenHealthy_ReturnsOkWithHealthyStatus()
    {
        _mockRepository.Setup(r => r.HealthCheckAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _controller.Get();

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);

        var responseType = okResult.Value!.GetType();
        var statusProperty = responseType.GetProperty("status");
        var timestampProperty = responseType.GetProperty("timestamp");

        Assert.NotNull(statusProperty);
        Assert.NotNull(timestampProperty);

        var statusValue = statusProperty.GetValue(okResult.Value) as string;
        Assert.Equal("healthy", statusValue);
    }

    [Fact]
    public async Task Get_WhenUnhealthy_ReturnsServiceUnavailable()
    {
        _mockRepository.Setup(r => r.HealthCheckAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _controller.Get();

        var statusCodeResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, statusCodeResult.StatusCode);
        Assert.NotNull(statusCodeResult.Value);

        var responseType = statusCodeResult.Value!.GetType();
        var statusProperty = responseType.GetProperty("status");
        var timestampProperty = responseType.GetProperty("timestamp");

        Assert.NotNull(statusProperty);
        Assert.NotNull(timestampProperty);

        var statusValue = statusProperty.GetValue(statusCodeResult.Value) as string;
        Assert.Equal("unhealthy", statusValue);
    }

    [Fact]
    public async Task Get_CallsRepositoryHealthCheck()
    {
        _mockRepository.Setup(r => r.HealthCheckAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await _controller.Get();

        _mockRepository.Verify(
            r => r.HealthCheckAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Get_WhenRepositoryThrows_PropagatesException()
    {
        _mockRepository.Setup(r => r.HealthCheckAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database connection failed"));

        await Assert.ThrowsAsync<Exception>(() => _controller.Get());
    }

    [Fact]
    public async Task Get_ReturnsTimestamp()
    {
        _mockRepository.Setup(r => r.HealthCheckAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var beforeCall = DateTime.UtcNow;
        var result = await _controller.Get();
        var afterCall = DateTime.UtcNow;

        var okResult = Assert.IsType<OkObjectResult>(result);

        var responseType = okResult.Value!.GetType();
        var timestampProperty = responseType.GetProperty("timestamp");
        Assert.NotNull(timestampProperty);

        var timestamp = (DateTime)timestampProperty.GetValue(okResult.Value)!;
        Assert.InRange(timestamp, beforeCall.AddSeconds(-1), afterCall.AddSeconds(1));
    }

    [Fact]
    public async Task Get_WithCancellationToken_PassesToRepository()
    {
        _mockRepository.Setup(r => r.HealthCheckAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _controller.Get();

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task Get_MultipleCallsWhenHealthy_ConsistentlyReturnsOk()
    {
        _mockRepository.Setup(r => r.HealthCheckAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        for (int i = 0; i < 5; i++)
        {
            var result = await _controller.Get();
            Assert.IsType<OkObjectResult>(result);
        }

        _mockRepository.Verify(
            r => r.HealthCheckAsync(It.IsAny<CancellationToken>()),
            Times.Exactly(5));
    }

    [Fact]
    public async Task Get_MultipleCallsWhenUnhealthy_ConsistentlyReturnsServiceUnavailable()
    {
        _mockRepository.Setup(r => r.HealthCheckAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        for (int i = 0; i < 3; i++)
        {
            var result = await _controller.Get();
            var statusCodeResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(503, statusCodeResult.StatusCode);
        }
    }

    [Fact]
    public async Task Get_StatusTransition_ReflectsRepositoryState()
    {
        // First call: healthy
        _mockRepository.Setup(r => r.HealthCheckAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result1 = await _controller.Get();
        Assert.IsType<OkObjectResult>(result1);

        // Second call: unhealthy
        _mockRepository.Setup(r => r.HealthCheckAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result2 = await _controller.Get();
        var statusCodeResult = Assert.IsType<ObjectResult>(result2);
        Assert.Equal(503, statusCodeResult.StatusCode);

        // Third call: healthy again
        _mockRepository.Setup(r => r.HealthCheckAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result3 = await _controller.Get();
        Assert.IsType<OkObjectResult>(result3);
    }

    [Fact]
    public async Task Get_ResponseIncludesCorrectStatusText()
    {
        _mockRepository.Setup(r => r.HealthCheckAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _controller.Get();
        var okResult = Assert.IsType<OkObjectResult>(result);

        var responseType = okResult.Value!.GetType();
        var statusProperty = responseType.GetProperty("status");
        Assert.NotNull(statusProperty);

        var statusValue = statusProperty.GetValue(okResult.Value) as string;
        Assert.Equal("healthy", statusValue);
    }

    [Fact]
    public async Task Get_UnhealthyResponseIncludesCorrectStatusText()
    {
        _mockRepository.Setup(r => r.HealthCheckAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _controller.Get();
        var statusCodeResult = Assert.IsType<ObjectResult>(result);

        var responseType = statusCodeResult.Value!.GetType();
        var statusProperty = responseType.GetProperty("status");
        Assert.NotNull(statusProperty);

        var statusValue = statusProperty.GetValue(statusCodeResult.Value) as string;
        Assert.Equal("unhealthy", statusValue);
    }
}
