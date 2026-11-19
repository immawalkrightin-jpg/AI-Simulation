using Simulation.API.Contracts.Ai;
using Simulation.API.Contracts.GroundTruth;
using WorldEngine.Sensors;
using WorldEngine.World;

namespace Simulation.API.Services;

public sealed record AdvanceResult(
    long Tick,
    GroundTruthSnapshot Snapshot,
    IReadOnlyList<Observation> Observations);

public sealed class SimulationSession
{
    private readonly object gate = new();
    private readonly WorldSimulation simulation;

    public SimulationSession(WorldSimulation simulation)
    {
        this.simulation = simulation;
    }

    public long Tick
    {
        get
        {
            lock (gate)
            {
                return simulation.State.Clock.Tick;
            }
        }
    }

    public GroundTruthSnapshot Snapshot()
    {
        lock (gate)
        {
            return GroundTruthMapper.Map(simulation);
        }
    }

    public AssetPicture Assets()
    {
        lock (gate)
        {
            var state = simulation.State;
            var sensors = simulation.Sensors?.Sensors ?? Array.Empty<Sensor>();
            return new AssetPicture(
                state.Clock.Tick,
                state.Width,
                state.Height,
                sensors
                    .Select(s => new AssetSensorDto(s.Id, s.Kind.ToString(), s.Position.X, s.Position.Y, s.Range))
                    .ToList());
        }
    }

    public AdvanceResult Advance(int ticks)
    {
        lock (gate)
        {
            simulation.Run(ticks);
            var tick = simulation.State.Clock.Tick;
            var observations = simulation.Sensors?.Collect(tick) ?? Array.Empty<Observation>();
            return new AdvanceResult(tick, GroundTruthMapper.Map(simulation), observations);
        }
    }
}
