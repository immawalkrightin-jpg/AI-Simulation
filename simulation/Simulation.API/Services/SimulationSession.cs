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
    private readonly Func<int, WorldSimulation>? factory;
    private WorldSimulation simulation;
    private int seed;

    public SimulationSession(WorldSimulation simulation, Func<int, WorldSimulation>? factory = null, int seed = 0)
    {
        this.simulation = simulation;
        this.factory = factory;
        this.seed = seed;
    }

    public bool CanReset => factory is not null;

    public int Seed
    {
        get
        {
            lock (gate)
            {
                return seed;
            }
        }
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

    public void Reset(int newSeed)
    {
        if (factory is null)
        {
            throw new InvalidOperationException("This session has no scenario factory to rebuild from.");
        }
        var rebuilt = factory(newSeed);
        lock (gate)
        {
            simulation = rebuilt;
            seed = newSeed;
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
