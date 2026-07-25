using Microsoft.AspNetCore.Mvc;

namespace FormaGeo.Api.Controllers;

[ApiController]
[Route("health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet(Name = "GetHealth")]
    [EndpointSummary("Check API health")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetHealth()
    {
        return Ok(new
        {
            status = "healthy",
            service = "FormaGeo.Api",
            timestamp = DateTimeOffset.UtcNow
        });
    }
}
