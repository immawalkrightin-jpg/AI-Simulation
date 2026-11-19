using Microsoft.AspNetCore.SignalR;
using Simulation.API.Contracts.Ai;
using Simulation.API.Hubs;

namespace Simulation.API.Services;

public sealed class SimulationBroadcaster
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly SimulationSession session;
    private readonly ObservationBuffer buffer;
    private readonly OperationalMap map;
    private readonly IHubContext<GroundTruthHub> groundTruthHub;
    private readonly IHubContext<ObservationHub> observationHub;
    private readonly IHubContext<AiHub> aiHub;

    public SimulationBroadcaster(
        SimulationSession session,
        ObservationBuffer buffer,
        OperationalMap map,
        IHubContext<GroundTruthHub> groundTruthHub,
        IHubContext<ObservationHub> observationHub,
        IHubContext<AiHub> aiHub)
    {
        this.session = session;
        this.buffer = buffer;
        this.map = map;
        this.groundTruthHub = groundTruthHub;
        this.observationHub = observationHub;
        this.aiHub = aiHub;
    }

    public async Task StepAsync(int ticks, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
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
        finally
        {
            gate.Release();
        }
    }

    public async Task ResetAsync(int seed, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            session.Reset(seed);
            buffer.Clear();
            var points = map.Clear();
            var snapshot = session.Snapshot();
            await groundTruthHub.Clients.All.SendAsync("snapshot", snapshot, cancellationToken);
            await aiHub.Clients.All.SendAsync("reset", new ClockDto(snapshot.Tick), cancellationToken);
            await aiHub.Clients.All.SendAsync("points", points, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }
}
