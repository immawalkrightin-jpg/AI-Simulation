using WorldEngine.Sensors;

namespace WorldEngine.World;

public sealed class WorldSimulation
{
    private readonly WorldActivityGenerator activity;

    public WorldState State { get; }
    public SensorNetwork? Sensors { get; }

    public WorldSimulation(
        WorldState state,
        WorldActivityGenerator? activity = null,
        SensorNetwork? sensors = null)
    {
        State = state;
        this.activity = activity ?? new WorldActivityGenerator();
        Sensors = sensors;
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
            unit.Step(State.Clock.TickSeconds);
        }
        Sensors?.Sense(State);
        activity.Step(State);
    }

    public void Run(int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            Step();
        }
    }
}
