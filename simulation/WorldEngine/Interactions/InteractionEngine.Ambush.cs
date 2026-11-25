using System.Numerics;
using WorldEngine.Countries;
using WorldEngine.Diplomacy;
using WorldEngine.Routing;
using WorldEngine.Territory;
using WorldEngine.Units;
using WorldEngine.World;

namespace WorldEngine.Interactions;

// Ambushes. On a front that has reached incident level, a country sends a few land combat units to wait
// just inside its own border, hidden from sensors. A hidden unit springs on a hostile convoy that drives
// past it, or on hostile land units that come close, and is revealed when it does.
public sealed partial class InteractionEngine
{
    public const double AmbushStrike = 0.5;
    public const int AmbushInterval = 20;
    public const int AmbushPatience = 150;
    public const int AmbushesPerCountry = 2;
    public const int MaxAmbushes = 6;
    public const double AmbushVictimDamage = 25;
    public const double AmbushRecoil = 6;
    private const float AmbushSearch = 160f;

    // Units on their way to a post: the tick they were sent, where the post is and whom they wait for.
    private readonly Dictionary<int, (long Since, Vector2 Post, CountryId Rival)> posting = new();

    private static bool Hostile(WorldState world, CountryId a, CountryId b) =>
        world.Relations.Get(a, b) < RouteNetwork.HostileThreshold;

    private void Ambush(WorldState world, long tick)
    {
        Settle(world, tick);
        foreach (var ambusher in world.Units.Where(u => u.Status == UnitStatus.Ambushing).ToList())
        {
            Strike(world, ambusher, tick);
        }
        if (tick % AmbushInterval == 0)
        {
            Arm(world, tick);
        }
    }

    private int Waiting(WorldState world, CountryId owner) =>
        world.Units.Count(u => u.Owner == owner && u.Status == UnitStatus.Ambushing)
        + posting.Keys.Count(id => world.GetUnit(id).Owner == owner);

    private void Arm(WorldState world, long tick)
    {
        if (world.Territory is not { } territory || world.Escalation is not { } ladder)
        {
            return;
        }
        foreach (var state in ladder.Fronts)
        {
            if (state.Rung < Rung.Incident)
            {
                continue;
            }
            var first = world.Countries.Get(state.Front.First);
            var second = world.Countries.Get(state.Front.Second);
            if (first.Landmass.Length == 0 || first.Landmass != second.Landmass)
            {
                continue;
            }
            if (world.Units.Count(u => u.Status == UnitStatus.Ambushing) + posting.Count >= MaxAmbushes)
            {
                return;
            }
            Post(world, territory, first, second, tick);
            Post(world, territory, second, first, tick);
        }
    }

    // Sends the unit closest to the rival's capital to the border and records where it is going.
    private void Post(WorldState world, TerritoryMap territory, Country own, Country rival, long tick)
    {
        if (Waiting(world, own.Id) >= AmbushesPerCountry)
        {
            return;
        }
        var candidate = world.Units
            .Where(u => u.Owner == own.Id
                && u.Status == UnitStatus.Idle
                && AmbushRules.CanAmbush(u.Kind)
                && !posting.ContainsKey(u.Id))
            .OrderBy(u => Vector2.DistanceSquared(u.Position, rival.Capital))
            .ThenBy(u => u.Id)
            .FirstOrDefault();
        if (candidate is null)
        {
            return;
        }
        if (territory.FindNearest(Domain.Land, rival.Id, candidate.Position, AmbushSearch) is not { } edge)
        {
            return;
        }
        var away = candidate.Position - edge;
        var length = away.Length();
        if (length < 1e-3f)
        {
            return;
        }
        var post = edge + away / length * (territory.CellSize * 1.5f);
        if (!territory.Permits(Domain.Land, own.Id, post) || !world.TryOrderMove(candidate, post))
        {
            return;
        }
        posting[candidate.Id] = (tick, post, rival.Id);
    }

    // Units that have reached their post take cover. Units that were sent elsewhere, destroyed or took
    // too long are forgotten.
    private void Settle(WorldState world, long tick)
    {
        foreach (var entry in posting.OrderBy(p => p.Key).ToList())
        {
            var unit = world.GetUnit(entry.Key);
            var (since, post, rival) = entry.Value;
            if (unit.Status == UnitStatus.Destroyed || tick - since > AmbushPatience)
            {
                posting.Remove(entry.Key);
                continue;
            }
            if (unit.Status != UnitStatus.Idle)
            {
                continue;
            }
            posting.Remove(entry.Key);
            if (Vector2.Distance(unit.Position, post) > 0.5f)
            {
                continue;
            }
            unit.Conceal();
            Log(
                world,
                "ambush_set",
                $"{world.Countries.Get(unit.Owner).Name} troops take cover inside their border, waiting for {world.Countries.Get(rival).Name}");
        }
    }

    private static string KindWord(UnitKind kind) => kind switch
    {
        UnitKind.ArmoredCar => "armored car",
        UnitKind.SupplyConvoy => "supply convoy",
        _ => kind.ToString().ToLowerInvariant()
    };

    // A truck leg covers 25 units a tick, more than the ambush radius, so the last tick's travel is checked
    // at three points rather than one.
    private static bool Passing(Interaction shipment, long tick, Vector2 post)
    {
        var span = Math.Max(1, shipment.DueTick - shipment.StartTick);
        foreach (var back in new[] { 1.0, 0.5, 0.0 })
        {
            var progress = Math.Clamp((tick - back - shipment.StartTick) / span, 0.0, 1.0);
            var at = shipment.Route!.At(progress);
            if (at.Carrier == UnitKind.Truck && Vector2.Distance(at.Point, post) <= AmbushRules.Radius)
            {
                return true;
            }
        }
        return false;
    }

    // One hidden unit looks for something to spring on. A convoy on a truck leg comes first, then land units.
    private void Strike(WorldState world, Unit ambusher, long tick)
    {
        var owner = world.Countries.Get(ambusher.Owner);
        bool Fair(CountryId party) =>
            party != ambusher.Owner && Hostile(world, ambusher.Owner, party) && !Forbidden(world, ambusher.Owner, party);

        foreach (var shipment in world.Interactions.Active)
        {
            if (shipment.Kind != InteractionKind.SupplyShipment
                || shipment.Route is null
                || shipment.ResolveTick <= tick
                || shipment.InterceptedBy is not null
                || (!Fair(shipment.Source) && !Fair(shipment.Target)))
            {
                continue;
            }
            if (!Passing(shipment, tick, ambusher.Position))
            {
                continue;
            }
            if (world.Chance.NextDouble() >= AmbushStrike)
            {
                continue;
            }
            var sender = world.Countries.Get(shipment.Source);
            var receiver = world.Countries.Get(shipment.Target);
            shipment.Intercept(owner.Id, tick);
            ambusher.ApplyDamage(AmbushRecoil);
            ambusher.Reveal();
            world.Relations.Adjust(owner.Id, sender.Id, -0.05);
            Log(
                world,
                "ambush_sprung",
                $"{owner.Name} troops ambush the {sender.Name} convoy bound for {receiver.Name}, because relations are hostile");
            return;
        }

        var victim = world.Units
            .Where(u => u.Owner != ambusher.Owner
                && u.Status != UnitStatus.Destroyed
                && u.Status != UnitStatus.Ambushing
                && u.Kind.DomainOf() == Domain.Land
                && Fair(u.Owner)
                && Vector2.Distance(u.Position, ambusher.Position) <= AmbushRules.Radius)
            .OrderBy(u => Vector2.DistanceSquared(u.Position, ambusher.Position))
            .ThenBy(u => u.Id)
            .FirstOrDefault();
        if (victim is null || world.Chance.NextDouble() >= AmbushStrike)
        {
            return;
        }
        var victimOwner = world.Countries.Get(victim.Owner);
        victim.ApplyDamage(AmbushVictimDamage + world.Chance.NextDouble() * 20);
        ambusher.ApplyDamage(AmbushRecoil);
        ambusher.Reveal();
        world.Relations.Adjust(owner.Id, victimOwner.Id, -0.04);
        Log(
            world,
            "ambush_sprung",
            $"{owner.Name} troops ambush a {victimOwner.Name} {KindWord(victim.Kind)} near the border, because relations are hostile");
    }
}
