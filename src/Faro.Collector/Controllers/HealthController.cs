using Faro.Storage;
using Microsoft.AspNetCore.Mvc;

namespace Faro.Collector.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController: ControllerBase
{
    private readonly IMetricsRepository _repository;

    public HealthController(IMetricsRepository repository)
    {
        _repository = repository;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Get()
    {
        var isHealthy = await _repository.HealthCheckAsync();
        if (isHealthy)
        {
            return Ok(new { status = "health", timestamp = DateTime.UtcNow });
        }

        return StatusCode(503, new { status = "unhealthy", timestamp = DateTime.UtcNow });
    }
}