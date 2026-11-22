using Microsoft.AspNetCore.Mvc;
using Simulation.API.Contracts.Feedback;
using Simulation.API.Services;

namespace Simulation.API.Controllers;

[ApiController]
[Route("api/feedback")]
public sealed class FeedbackController : ControllerBase
{
    private const int MaxRecent = 200;

    private readonly FeedbackStore store;
    private readonly SimulationSession session;

    public FeedbackController(FeedbackStore store, SimulationSession session)
    {
        this.store = store;
        this.session = session;
    }

    [HttpGet("categories")]
    public IActionResult Categories() => Ok(FeedbackStore.Categories);

    [HttpGet("corrections")]
    public IActionResult Recent([FromQuery] int count = 20)
    {
        if (count < 1 || count > MaxRecent)
        {
            return BadRequest(new { error = $"count must be between 1 and {MaxRecent}." });
        }
        return Ok(store.Recent(count));
    }

    [HttpPost("corrections")]
    public IActionResult Add([FromBody] CorrectionInput input)
    {
        var result = store.Add(input, session.Tick, session.Seed);
        if (result.Error is not null)
        {
            return BadRequest(new { error = result.Error });
        }
        return Ok(result.Correction);
    }
}
