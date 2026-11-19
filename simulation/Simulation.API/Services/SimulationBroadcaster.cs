using Microsoft.AspNetCore.SignalR;
using Simulation.API.Contracts.Ai;
using Simulation.API.Hubs;

namespace Simulation.API.Services;

public sealed class SimulationBroadcaster
{
    private readonly SimulationSession session;
    private readonly ObservationBuffer buffer;
    private readonly IHubContext<GroundTruthHub> groundTruthHub;
    private readonly IHubContext<ObservationHub> observationHub;
    private readonly IHubContext<AiHub> aiHub;

    public SimulationBroadcaster(
        SimulationSession session,
        ObservationBuffer buffer,
        IHubContext<GroundTruthHub> groundTruthHub,
        IHubContext<ObservationHub> observationHub,
        IHubContext<AiHub> aiHub)
    {
        this.session = session;
        this.buffer = buffer;
        this.groundTruthHub = groundTruthHub;
        this.observationHub = observationHub;
        this.aiHub = aiHub;
    }

    public async Task StepAsync(int ticks, CancellationToken cancellationToken = default)
    {
        var result = session.Advance(ticks);
        var batch = buffer.Append(result.Tick, result.Observations);
        await groundTruthHub.Clients.All.SendAsync("snapshot", result.Snapshot, cancellationToken);
        await aiHub.Clients.All.SendAsync("clock", new ClockDto(result.Tick), cancellationToken);
        if (batch.Observations.Count > 0)
        {
            await observationHub.Clients.All.SendAsync("observations", batch, cancellationToken);
        }
    }
}
