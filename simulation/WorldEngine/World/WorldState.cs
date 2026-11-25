using System.Numerics;
using WorldEngine.Countries;
using WorldEngine.Diplomacy;
using WorldEngine.Events;
using WorldEngine.Interactions;
using WorldEngine.Routing;
using WorldEngine.SimulationClock;
using WorldEngine.Territory;
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
    public InteractionBook Interactions { get; } = new();
    public CountryId? ModelCountry { get; set; }
    public TerritoryMap? Territory { get; set; }
    public RouteNetwork? Routes { get; set; }
    public Doctrine? Doctrine { get; set; }
    public EscalationLadder? Escalation { get; set; }
    public ZoneBook Zones { get; } = new();
    public OccupationBook Occupations { get; } = new();
    public Random Rng { get; }

    // A second stream for ambushes and conquest, so those never shift the draws of the main one.
    public Random Chance { get; }
    public float Width { get; }
    public float Height { get; }

    public WorldState(int seed, float width, float height, double tickSeconds = 60)
    {
        Rng = new Random(seed);
        Chance = new Random(seed ^ 0x5EED5EED);
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

    public Unit AddUnit(CountryId owner, UnitKind kind, Vector2 position)
    {
        var spec = UnitCatalog.Of(kind);
        return AddUnit(owner, kind, position, spec.Speed, spec.Strength);
    }

    public Unit GetUnit(int id) =>
        units.TryGetValue(id, out var unit)
            ? unit
            : throw new KeyNotFoundException($"Unknown unit {id}.");

    public Vector2 RandomPoint() =>
        new((float)(Rng.NextDouble() * Width), (float)(Rng.NextDouble() * Height));

    // A unit lying in ambush holds its ground. A unit in a paralyzed patch of its own country cannot move.
    // Land units never march onto ground an enemy holds, and a garrison stays inside the patch it holds.
    public bool CanMove(Unit unit, Vector2 destination)
    {
        if (unit.Status == UnitStatus.Ambushing)
        {
            return false;
        }
        var tick = Clock.Tick;
        if (Zones.Blocks(unit.Owner, unit.Position, tick) || Zones.Blocks(unit.Owner, destination, tick))
        {
            return false;
        }
        if (Territory is null)
        {
            return true;
        }
        var domain = unit.Kind.DomainOf();
        if (domain == Domain.Land)
        {
            var here = Occupations.HolderAt(unit.Position);
            var there = Occupations.HolderAt(destination);
            if (there is { } holder && holder != unit.Owner)
            {
                return false;
            }
            if (here == unit.Owner)
            {
                return there == unit.Owner;
            }
        }
        return Territory.SegmentPermits(domain, unit.Owner, unit.Position, destination);
    }

    public bool TryOrderMove(Unit unit, Vector2 destination)
    {
        if (!CanMove(unit, destination))
        {
            return false;
        }
        unit.OrderMove(destination);
        return true;
    }
}
