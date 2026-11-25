using WorldEngine.Interactions;
using WorldEngine.Sensors;
using WorldEngine.Units;

namespace WorldEngine.World;

public sealed class WorldSimulation
{
    private readonly WorldActivityGenerator activity;
    private readonly InteractionEngine? interactions;

    public WorldState State { get; }
    public SensorNetwork? Sensors { get; }

    public WorldSimulation(
        WorldState state,
        WorldActivityGenerator? activity = null,
        SensorNetwork? sensors = null,
        InteractionEngine? interactions = null)
    {
        State = state;
        this.activity = activity ?? new WorldActivityGenerator();
        Sensors = sensors;
        this.interactions = interactions;
    }

    public void Step()
    {
        State.Clock.Advance();
        foreach (var worldEvent in State.Scheduler.TakeDue(State.Clock.Tick))
        {
            worldEvent.Apply(State);
            State.Log.Append(State.Clock.Tick, worldEvent);
        }
        foreach (var unit in State.Units)
        {
            if (unit.Status == UnitStatus.Moving && State.Zones.Blocks(unit.Owner, unit.Position, State.Clock.Tick))
            {
                unit.Halt();
            }
            unit.Step(State.Clock.TickSeconds);
        }
        Sensors?.Sense(State);
        activity.Step(State);
        interactions?.Step(State);
    }

    public void Run(int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            Step();
        }
    }
}
