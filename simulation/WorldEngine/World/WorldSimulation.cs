namespace WorldEngine.World;

public sealed class WorldSimulation
{
    private readonly WorldActivityGenerator activity;

    public WorldState State { get; }

    public WorldSimulation(WorldState state, WorldActivityGenerator? activity = null)
    {
        State = state;
        this.activity = activity ?? new WorldActivityGenerator();
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
