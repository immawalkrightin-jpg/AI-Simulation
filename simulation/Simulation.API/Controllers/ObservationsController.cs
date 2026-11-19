using Microsoft.AspNetCore.Mvc;
using Simulation.API.Services;

namespace Simulation.API.Controllers;

[ApiController]
[Route("api/observations")]
public sealed class ObservationsController : ControllerBase
{
    private readonly ObservationBuffer buffer;

    public ObservationsController(ObservationBuffer buffer)
    {
        this.buffer = buffer;
    }

    [HttpGet]
    public IActionResult Get([FromQuery] long after = 0) => Ok(buffer.ReadAfter(after));
}
