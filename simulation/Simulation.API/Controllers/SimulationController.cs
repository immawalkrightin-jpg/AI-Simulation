using Microsoft.AspNetCore.Mvc;
using Simulation.API.Contracts.GroundTruth;
using Simulation.API.Services;

namespace Simulation.API.Controllers;

[ApiController]
[Route("api/simulation")]
public sealed class SimulationController : ControllerBase
{
    private const int MaxStepCount = 1000;

    private readonly SimulationSession session;
    private readonly SimulationControl control;
    private readonly SimulationBroadcaster broadcaster;

    public SimulationController(
        SimulationSession session,
        SimulationControl control,
        SimulationBroadcaster broadcaster)
    {
        this.session = session;
        this.control = control;
        this.broadcaster = broadcaster;
    }

    [HttpGet("status")]
    public IActionResult Status() => Ok(CurrentStatus());

    [HttpPost("pause")]
    public IActionResult Pause()
    {
        control.Paused = true;
        return Ok(CurrentStatus());
    }

    [HttpPost("resume")]
    public IActionResult Resume()
    {
        control.Paused = false;
        return Ok(CurrentStatus());
    }

    [HttpPost("step")]
    public async Task<IActionResult> Step([FromQuery] int count = 1, CancellationToken cancellationToken = default)
    {
        if (count < 1 || count > MaxStepCount)
        {
            return BadRequest($"count must be between 1 and {MaxStepCount}.");
        }
        await broadcaster.StepAsync(count, cancellationToken);
        return Ok(CurrentStatus());
    }

    private SimulationStatus CurrentStatus() => new(session.Tick, control.Paused);
}
