using WorldEngine.World;

namespace WorldEngine.Events;

public abstract class WorldEvent
{
    public long ScheduledTick { get; init; }
    public abstract string Kind { get; }
    public abstract void Apply(WorldState world);
    public abstract string Describe();
}
