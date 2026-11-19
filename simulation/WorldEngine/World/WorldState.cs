using System.Numerics;
using WorldEngine.Countries;
using WorldEngine.Diplomacy;
using WorldEngine.Events;
using WorldEngine.SimulationClock;
using WorldEngine.Units;

namespace WorldEngine.World;

public sealed class WorldState
{
    private readonly Dictionary<int, Unit> units = new();
    private int nextUnitId = 1;

    public SimClock Clock { get; }
    public CountryRegistry Countries { get; } = new();
    public RelationMatrix Relations { get; } = new();
    public EventScheduler Scheduler { get; } = new();
    public EventLog Log { get; } = new();
    public Random Rng { get; }
    public float Width { get; }
    public float Height { get; }

    public WorldState(int seed, float width, float height, double tickSeconds = 60)
    {
        Rng = new Random(seed);
        Width = width;
        Height = height;
        Clock = new SimClock(tickSeconds);
    }

    public IReadOnlyList<Unit> Units => units.Values.OrderBy(u => u.Id).ToList();

    public Unit AddUnit(CountryId owner, UnitKind kind, Vector2 position, double speed, double strength)
    {
        if (!Countries.Contains(owner))
        {
            throw new KeyNotFoundException($"Unknown country {owner}.");
        }
        var unit = new Unit(nextUnitId++, owner, kind, position, speed, strength);
        units.Add(unit.Id, unit);
        return unit;
    }

    public Unit GetUnit(int id) =>
        units.TryGetValue(id, out var unit)
            ? unit
            : throw new KeyNotFoundException($"Unknown unit {id}.");

    public Vector2 RandomPoint() =>
        new((float)(Rng.NextDouble() * Width), (float)(Rng.NextDouble() * Height));
}
