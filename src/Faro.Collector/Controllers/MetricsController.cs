using FluentValidation;
using Faro.Collector.Services;
using Faro.Shared.Models;
using Microsoft.AspNetCore.Mvc;

namespace Faro.Collector.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MetricsController: ControllerBase
{
    private readonly MetricsBuffer _buffer;
    private readonly IValidator<MetricPoint> _validator;
    private readonly ILogger<MetricsController> _logger;

    public MetricsController(
        MetricsBuffer buffer,
        IValidator<MetricPoint> validator,
        ILogger<MetricsController> logger)
    {
        _buffer = buffer;
        _validator = validator;
        _logger = logger;
    }

    /// <summary>
    /// Submit a single metric
    /// </summary>
    [HttpPost("single")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PostSingle([FromBody] MetricPoint metric)
    {
        var validationResult = await _validator.ValidateAsync(metric);
        if (!validationResult.IsValid)
        {
            return BadRequest(validationResult.Errors);
        }

        await _buffer.AddMetricAsync(metric);
        return Accepted(new { received = 1 });
    }

    /// <summary>
    /// Submit a batch of metrics
    /// </summary>
    [HttpPost("batch")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PostBatch([FromBody] MetricBatch batch)
    {
        if (batch.Metrics == null || !batch.Metrics.Any())
        {
            return BadRequest("Batch must contain at least one metric");
        }

        var validationErrors = new List<string>();

        foreach (var metric in batch.Metrics)
        {
            var validationResult = await _validator.ValidateAsync(metric);
            if (!validationResult.IsValid)
            {
                validationErrors.AddRange(
                    validationResult.Errors.Select(e => $"{metric.MetricName}: {e.ErrorMessage}"));
            }
        }

        if (validationErrors.Any())
        {
            return BadRequest(new { errors = validationErrors });
        }

        foreach (var metric in batch.Metrics)
        {
            await _buffer.AddMetricAsync(metric);
        }

        _logger.LogInformation("Received batch of {Count} metrics", batch.Metrics.Count);
        return Accepted(new { received = batch.Metrics.Count });
    }
}