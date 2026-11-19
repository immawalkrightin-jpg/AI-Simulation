using Microsoft.AspNetCore.Mvc;
using Simulation.API.Services;

namespace Simulation.API.Controllers;

[ApiController]
[Route("api/ground-truth")]
public sealed class GroundTruthController : ControllerBase
{
    private readonly SimulationSession session;

    public GroundTruthController(SimulationSession session)
    {
        this.session = session;
    }

    [HttpGet("snapshot")]
    public IActionResult Snapshot() => Ok(session.Snapshot());
}
