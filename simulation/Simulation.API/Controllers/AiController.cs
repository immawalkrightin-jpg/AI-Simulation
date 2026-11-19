using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Simulation.API.Contracts.Ai;
using Simulation.API.Hubs;
using Simulation.API.Services;

namespace Simulation.API.Controllers;

[ApiController]
[Route("api/ai")]
public sealed class AiController : ControllerBase
{
    private readonly SimulationSession session;
    private readonly OperationalMap map;
    private readonly IHubContext<AiHub> hub;

    public AiController(SimulationSession session, OperationalMap map, IHubContext<AiHub> hub)
    {
        this.session = session;
        this.map = map;
        this.hub = hub;
    }

    [HttpGet("assets")]
    public IActionResult Assets() => Ok(session.Assets());

    [HttpGet("points")]
    public IActionResult Points() => Ok(map.Snapshot());

    [HttpPut("points/{id}")]
    public async Task<IActionResult> Put(string id, [FromBody] OperationalPointInput input)
    {
        var assets = session.Assets();
        var result = map.Upsert(id, input, assets.Tick, assets.Width, assets.Height);
        if (result.Error is not null)
        {
            return BadRequest(new { error = result.Error });
        }
        await hub.Clients.All.SendAsync("points", result.Map);
        return Ok(result.Map);
    }

    [HttpDelete("points/{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var result = map.Remove(id);
        if (result is null)
        {
            return NotFound();
        }
        await hub.Clients.All.SendAsync("points", result);
        return Ok(result);
    }
}
