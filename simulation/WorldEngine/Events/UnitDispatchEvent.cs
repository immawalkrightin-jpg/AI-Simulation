using System.Numerics;
using WorldEngine.World;

namespace WorldEngine.Events;

public sealed class UnitDispatchEvent : WorldEvent
{
    public int UnitId { get; }
    public Vector2 Destination { get; }

    public override string Kind => "unit_dispatch";

    public UnitDispatchEvent(int unitId, Vector2 destination)
    {
        UnitId = unitId;
        Destination = destination;
    }

    public override void Apply(WorldState world) => world.GetUnit(UnitId).OrderMove(Destination);

    public override string Describe() =>
        $"unit {UnitId} dispatched to ({Destination.X:0},{Destination.Y:0})";
}
