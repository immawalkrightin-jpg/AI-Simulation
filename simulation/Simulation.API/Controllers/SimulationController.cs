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

    [HttpPost("speed")]
    public IActionResult Speed([FromQuery] double value = 1)
    {
        if (double.IsNaN(value) || value < SimulationControl.MinSpeed || value > SimulationControl.MaxSpeed)
        {
            return BadRequest($"value must be between {SimulationControl.MinSpeed} and {SimulationControl.MaxSpeed}.");
        }
        control.Speed = value;
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

    [HttpPost("reset")]
    public async Task<IActionResult> Reset([FromQuery] int? seed = null, CancellationToken cancellationToken = default)
    {
        if (!session.CanReset)
        {
            return Conflict("This simulation cannot be reset.");
        }
        var target = seed ?? session.Seed;
        if (target < 0)
        {
            return BadRequest("seed must not be negative.");
        }
        await broadcaster.ResetAsync(target, cancellationToken);
        return Ok(CurrentStatus());
    }

    private SimulationStatus CurrentStatus() => new(session.Tick, control.Paused, session.Seed, control.Speed);
}
