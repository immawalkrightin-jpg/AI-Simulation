using System.Numerics;
using WorldEngine.Countries;
using WorldEngine.Territory;
using WorldEngine.Units;
using WorldEngine.World;

namespace WorldEngine.Interactions;

public sealed partial class InteractionEngine
{
    public const double ConquestChance = 0.6;
    public const double ConquestMargin = 1.15;
    public const int MaxOccupationsPerPair = 2;
    public const int GarrisonSize = 2;
    public const long MinimumHold = 30;
    public const double OverrunDamage = 30;
    public const double OccupationIncomeLoss = 0.1;
    private const float GarrisonSearch = 120f;
    private const float OccupationSearch = 400f;

    private readonly Dictionary<int, int> garrisons = new();

    private static bool IsGarrisonKind(UnitKind kind)
    {
        var spec = UnitCatalog.Of(kind);
        return spec.Domain == Domain.Land && spec.Role == UnitRole.Combat;
    }

    private void Conquer(WorldState world, Country a, Country b, long tick)
    {
        if (world.Doctrine is null || world.Territory is not { } territory)
        {
            return;
        }
        if (a.Landmass.Length == 0 || a.Landmass != b.Landmass)
        {
            return;
        }
        var powerA = NationalPower.Of(world, a.Id).Total;
        var powerB = NationalPower.Of(world, b.Id).Total;
        Country winner;
        Country loser;
        if (powerA >= powerB * ConquestMargin)
        {
            (winner, loser) = (a, b);
        }
        else if (powerB >= powerA * ConquestMargin)
        {
            (winner, loser) = (b, a);
        }
        else
        {
            return;
        }
        if (world.Occupations.Count(winner.Id, loser.Id) >= MaxOccupationsPerPair)
        {
            return;
        }
        if (world.Chance.NextDouble() >= ConquestChance)
        {
            return;
        }
        if (territory.FindNearest(Domain.Land, loser.Id, winner.Capital, OccupationSearch) is not { } edge)
        {
            return;
        }
        var radius = 10f + (float)(world.Chance.NextDouble() * 6);
        var inward = loser.Capital - edge;
        var length = inward.Length();
        var center = length < 1e-3f ? edge : edge + inward / length * (radius * 0.6f);
        if (!territory.Permits(Domain.Land, loser.Id, center))
        {
            center = edge;
        }
        if (world.Occupations.All.Any(o => Vector2.Distance(o.Center, center) < o.Radius + radius))
        {
            return;
        }
        var cause = $"{winner.Name} outmatched {loser.Name} in the fighting";
        var occupation = world.Occupations.Add(winner.Id, loser.Id, center, radius, tick, cause);
        foreach (var unit in world.Units.Where(u =>
                     u.Owner == loser.Id
                     && u.Status != UnitStatus.Destroyed
                     && u.Kind.DomainOf() == Domain.Land
                     && occupation.Contains(u.Position)))
        {
            unit.ApplyDamage(OverrunDamage);
        }
        world.Relations.Adjust(winner.Id, loser.Id, -0.1);
        Log(
            world,
            "territory_seized",
            $"{winner.Name} seized ground inside {loser.Name} after a decisive clash, because {cause}");
    }

    private void ReturnGround(WorldState world, Country a, Country b, long tick)
    {
        while (world.Occupations.Release(a.Id, b.Id, tick, MinimumHold) is { } released)
        {
            var holder = world.Countries.Get(released.Holder);
            var loser = world.Countries.Get(released.Loser);
            PullBack(world, released);
            world.Relations.Adjust(holder.Id, loser.Id, 0.05);
            Log(
                world,
                "ground_returned",
                $"{holder.Name} hands the ground it seized back to {loser.Name}, because the fighting has ended");
        }
    }

    private void PullBack(WorldState world, Occupation released)
    {
        var territory = world.Territory;
        var spread = 0;
        foreach (var unit in world.Units.Where(u =>
                     u.Owner == released.Holder
                     && u.Kind.DomainOf() == Domain.Land))
        {
            var wreck = unit.Status == UnitStatus.Destroyed;
            var assigned = garrisons.TryGetValue(unit.Id, out var id) && id == released.Id;
            if (assigned)
            {
                garrisons.Remove(unit.Id);
                unit.Halt();
            }
            if (territory is null)
            {
                continue;
            }
            var standing = territory.Permits(Domain.Land, unit.Owner, unit.Position)
                || (world.Occupations.HolderAt(unit.Position) == unit.Owner && !territory.IsSea(unit.Position));
            if (standing)
            {
                // An order given while the ground was held may now lead onto ground that has been returned.
                if (unit.Destination is { } goal && !world.HeldPath(unit.Owner, unit.Position, goal))
                {
                    unit.Halt();
                }
                continue;
            }
            var home = territory.FindNearest(Domain.Land, released.Holder, released.Center, GarrisonSearch)
                ?? territory.FindNearest(Domain.Land, released.Holder, unit.Position, OccupationSearch);
            if (home is { } point)
            {
                var spot = point + new Vector2(spread++ * 2.2f, 0);
                var landing = territory.Permits(Domain.Land, unit.Owner, spot) ? spot : point;
                if (wreck)
                {
                    unit.Recover(landing);
                }
                else
                {
                    unit.Relocate(landing);
                }
            }
        }
    }

    private void AdvanceGarrisons(WorldState world)
    {
        var live = world.Occupations.All.ToList();
        if (live.Count == 0 && garrisons.Count == 0)
        {
            return;
        }
        foreach (var pair in garrisons.OrderBy(p => p.Key).ToList())
        {
            var unit = world.GetUnit(pair.Key);
            if (live.All(o => o.Id != pair.Value) || unit.Status != UnitStatus.Moving)
            {
                garrisons.Remove(pair.Key);
            }
        }
        if (world.Territory is not { } territory)
        {
            return;
        }
        foreach (var occupation in live)
        {
            var units = world.Units;
            var present = units.Count(u =>
                u.Owner == occupation.Holder
                && u.Status != UnitStatus.Destroyed
                && IsGarrisonKind(u.Kind)
                && occupation.Contains(u.Position));
            var marching = garrisons.Count(g => g.Value == occupation.Id);
            if (present + marching >= GarrisonSize)
            {
                continue;
            }
            var target = occupation.Center + new Vector2((present + marching) * 2.5f, 0);
            if (!territory.Permits(Domain.Land, occupation.Loser, target) || !occupation.Contains(target))
            {
                target = occupation.Center;
            }
            var candidate = units
                .Where(u => u.Owner == occupation.Holder
                    && u.Status == UnitStatus.Idle
                    && IsGarrisonKind(u.Kind)
                    && !posting.ContainsKey(u.Id)
                    && !garrisons.ContainsKey(u.Id)
                    && !occupation.Contains(u.Position)
                    && Vector2.Distance(u.Position, occupation.Center) <= GarrisonSearch)
                .OrderBy(u => Vector2.DistanceSquared(u.Position, occupation.Center))
                .ThenBy(u => u.Id)
                .FirstOrDefault(u => world.HeldPath(u.Owner, u.Position, target));
            if (candidate is null)
            {
                continue;
            }
            candidate.OrderMove(target);
            garrisons[candidate.Id] = occupation.Id;
        }
    }
}
