using System.Numerics;
using WorldEngine.Countries;

namespace WorldEngine.Territory;

// A patch of a country's land that a strike has put out of use. Its severity fades linearly to zero at the repair tick.
public sealed class ParalyzedZone
{
    public ParalyzedZone(
        int id,
        CountryId owner,
        Vector2 center,
        float radius,
        double severity,
        long startTick,
        long repairTick,
        string cause)
    {
        Id = id;
        Owner = owner;
        Center = center;
        Radius = radius;
        Severity = Math.Clamp(severity, 0.0, 1.0);
        StartTick = startTick;
        RepairTick = Math.Max(startTick + 1, repairTick);
        Cause = cause;
    }

    public int Id { get; }
    public CountryId Owner { get; }
    public Vector2 Center { get; }
    public float Radius { get; }
    public double Severity { get; }
    public long StartTick { get; }
    public long RepairTick { get; }
    public string Cause { get; }

    public bool Contains(Vector2 point) => Vector2.DistanceSquared(point, Center) <= Radius * Radius;

    public double SeverityAt(long tick)
    {
        if (tick >= RepairTick)
        {
            return 0.0;
        }
        var span = Math.Max(1, RepairTick - StartTick);
        return Severity * (1.0 - Math.Clamp((tick - StartTick) / (double)span, 0.0, 1.0));
    }
}

public sealed class ZoneBook
{
    // A zone this bad keeps the owner's units and depots idle. Below it the owner is only slowed.
    public const double BlockSeverity = 0.5;

    private readonly List<ParalyzedZone> items = new();
    private int nextId = 1;

    public ParalyzedZone Add(
        CountryId owner,
        Vector2 center,
        float radius,
        double severity,
        long startTick,
        long repairTick,
        string cause)
    {
        var zone = new ParalyzedZone(nextId++, owner, center, radius, severity, startTick, repairTick, cause);
        items.Add(zone);
        return zone;
    }

    public IReadOnlyList<ParalyzedZone> Active(long tick) =>
        items.Where(z => tick < z.RepairTick).ToList();

    // The worst damage any live zone of this country is doing right now, from 0 (none) to 1 (fully out of use).
    public double Severity(CountryId owner, long tick)
    {
        var worst = 0.0;
        foreach (var zone in items)
        {
            if (zone.Owner == owner)
            {
                worst = Math.Max(worst, zone.SeverityAt(tick));
            }
        }
        return worst;
    }

    public bool Covers(Vector2 point, long tick) =>
        items.Any(z => tick < z.RepairTick && z.Contains(point));

    // True when a live zone of this owner covers the point and is still bad enough to stop its units.
    public bool Blocks(CountryId owner, Vector2 point, long tick)
    {
        foreach (var zone in items)
        {
            if (zone.Owner == owner && zone.Contains(point) && zone.SeverityAt(tick) >= BlockSeverity)
            {
                return true;
            }
        }
        return false;
    }

    // Removes and returns the zones that are fully repaired by this tick.
    public IReadOnlyList<ParalyzedZone> TakeRepaired(long tick)
    {
        var done = items.Where(z => z.RepairTick <= tick).ToList();
        if (done.Count > 0)
        {
            items.RemoveAll(z => z.RepairTick <= tick);
        }
        return done;
    }
}
