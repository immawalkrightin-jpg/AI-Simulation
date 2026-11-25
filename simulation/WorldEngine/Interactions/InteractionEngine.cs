using System.Numerics;
using WorldEngine.Countries;
using WorldEngine.Diplomacy;
using WorldEngine.Routing;
using WorldEngine.Units;
using WorldEngine.World;

namespace WorldEngine.Interactions;

public sealed class InteractionEngine
{
    public const int FadeTicks = 8;
    public const double GauntletChance = 0.35;
    public const double HuntBase = 0.06;
    public const double HuntHostility = 0.08;
    public const double ShippingBlockSeverity = 0.5;
    private const double ClashThreshold = -0.7;
    private const double MissileThreshold = -0.8;

    private readonly HashSet<(string, string)> noDirect = new();
    private readonly double spawnChance;
    private readonly int maxActive;
    private Dictionary<(string, string), double>? baseline;

    public InteractionEngine(
        IEnumerable<(CountryId, CountryId)>? noDirectConflict = null,
        double spawnChance = 0.55,
        int maxActive = 14)
    {
        foreach (var (first, second) in noDirectConflict ?? Array.Empty<(CountryId, CountryId)>())
        {
            noDirect.Add(Key(first, second));
        }
        this.spawnChance = spawnChance;
        this.maxActive = maxActive;
    }

    public void Step(WorldState world)
    {
        baseline ??= CaptureBaseline(world);
        var tick = world.Clock.Tick;
        if (world.Doctrine is { } doctrine)
        {
            world.Escalation ??= new EscalationLadder(doctrine);
            Hunt(world, tick);
        }
        foreach (var due in world.Interactions.Active.Where(i => i.ResolveTick <= tick).ToList())
        {
            Resolve(world, due, tick);
        }
        if (tick % 5 == 0)
        {
            Revert(world);
        }
        if (tick % 10 == 0)
        {
            foreach (var country in world.Countries.All)
            {
                var factor = 1.0 - 0.75 * world.Zones.Severity(country.Id, tick);
                country.AddSupplies(8 * factor);
                country.AddAmmunition(3);
            }
        }
        foreach (var repaired in world.Zones.TakeRepaired(tick))
        {
            Log(world, "zone_repaired", $"{world.Countries.Get(repaired.Owner).Name} has repaired the damage left by the strike");
        }
        if (world.Escalation is { } ladder && tick % EscalationLadder.CheckInterval == 0)
        {
            foreach (var move in ladder.Step(world))
            {
                ApplyMove(world, move, "no new incidents for a long while");
            }
        }
        Spawn(world, tick);
        world.Interactions.Prune(tick, FadeTicks * 2);
    }

    private static (string, string) Key(CountryId a, CountryId b) =>
        string.CompareOrdinal(a.Value, b.Value) <= 0 ? (a.Value, b.Value) : (b.Value, a.Value);

    private static T Pick<T>(Random rng, IReadOnlyList<T> list) => list[rng.Next(list.Count)];

    private bool Forbidden(WorldState world, CountryId a, CountryId b) =>
        noDirect.Contains(Key(a, b)) || (world.Doctrine?.Forbids(a, b) ?? false);

    private static Dictionary<(string, string), double> CaptureBaseline(WorldState world)
    {
        var result = new Dictionary<(string, string), double>();
        var all = world.Countries.All;
        for (var i = 0; i < all.Count; i++)
        {
            for (var j = i + 1; j < all.Count; j++)
            {
                result[Key(all[i].Id, all[j].Id)] = world.Relations.Get(all[i].Id, all[j].Id);
            }
        }
        return result;
    }

    private void Revert(WorldState world)
    {
        foreach (var ((first, second), target) in baseline!)
        {
            var a = new CountryId(first);
            var b = new CountryId(second);
            world.Relations.Adjust(a, b, (target - world.Relations.Get(a, b)) * 0.03);
        }
    }

    private static void Log(WorldState world, string kind, string text) =>
        world.Log.Append(world.Clock.Tick, new InteractionEvent(kind, text));

    private static bool Busy(WorldState world, InteractionKind kind, CountryId a, CountryId b) =>
        world.Interactions.Active.Any(i => i.Kind == kind && Key(i.Source, i.Target) == Key(a, b));

    private static string Lower(Rung rung) => rung.ToString().ToLowerInvariant();

    private static void ApplyMove(WorldState world, LadderMove move, string coolingCause)
    {
        var front = move.State.Front;
        var a = world.Countries.Get(front.First);
        var b = world.Countries.Get(front.Second);
        if (move.To > move.From)
        {
            world.Relations.Adjust(
                a.Id,
                b.Id,
                move.To switch { Rung.Tension => -0.03, Rung.Incident => -0.05, _ => 0.0 });
            var text = move.To switch
            {
                Rung.Tension => $"tension rises on the {front.Name}: {a.Name} and {b.Name} trade accusations, because {front.Reason}",
                Rung.Incident => $"incident on the {front.Name}: {a.Name} and {b.Name} patrols exchange fire, because {front.Reason}",
                Rung.Clash => $"the {front.Name} escalates: {a.Name} and {b.Name} commit forces after repeated incidents",
                _ => $"the {front.Name} reaches missile level: {a.Name} and {b.Name} authorize strikes after the clashes"
            };
            Log(world, "front_escalated", text);
            return;
        }
        world.Relations.Adjust(a.Id, b.Id, 0.04);
        Log(world, "front_cooled", $"the {front.Name} cools to {Lower(move.To)}: {coolingCause}");
    }

    private void Spawn(WorldState world, long tick)
    {
        if (world.Interactions.Active.Count >= maxActive || world.Rng.NextDouble() > spawnChance)
        {
            return;
        }
        var roll = world.Rng.NextDouble();
        if (roll < 0.40)
        {
            Negotiate(world, tick);
        }
        else if (roll < 0.75)
        {
            Ship(world, tick);
        }
        else if (roll < 0.90)
        {
            Clash(world, tick);
        }
        else
        {
            Missile(world, tick);
        }
    }

    private static string Subject(Random rng, Country a, Country b)
    {
        string[] options;
        if (a.Bloc == b.Bloc && a.Bloc != Bloc.NonAligned)
        {
            options = new[] { "joint defense planning", "arms procurement", "coordinated trade" };
        }
        else if (a.Bloc != Bloc.NonAligned && b.Bloc != Bloc.NonAligned)
        {
            options = new[] { "arms limitation", "trade quotas", "border incident review" };
        }
        else
        {
            options = new[] { "trade agreement", "non-alignment consultations", "technical aid" };
        }
        return options[rng.Next(options.Length)];
    }

    private void Negotiate(WorldState world, long tick)
    {
        if (world.Doctrine is { } doctrine)
        {
            NegotiateByDoctrine(world, tick, doctrine);
            return;
        }
        var rng = world.Rng;
        var countries = world.Countries.All;
        var a = Pick(rng, countries);
        var others = countries
            .Where(c => c.Id != a.Id && !Busy(world, InteractionKind.Negotiation, a.Id, c.Id))
            .ToList();
        if (others.Count == 0)
        {
            return;
        }
        var b = Pick(rng, others);
        var chance = Math.Clamp(0.55 + world.Relations.Get(a.Id, b.Id) * 0.35, 0.1, 0.9);
        var duration = rng.Next(12, 30);
        var succeeds = rng.NextDouble() < chance;
        var resolve = tick + (succeeds ? duration : Math.Max(3, (int)(duration * (0.3 + rng.NextDouble() * 0.5))));
        var subject = Subject(rng, a, b);
        world.Interactions.Open(
            InteractionKind.Negotiation, a.Id, b.Id, TransportMode.None, 0, subject, tick, tick + duration, resolve,
            succeeds ? InteractionStatus.Succeeded : InteractionStatus.Cancelled);
        Log(world, "negotiation_started", $"{a.Name} opens talks with {b.Name}: {subject}");
    }

    private void NegotiateByDoctrine(WorldState world, long tick, Doctrine doctrine)
    {
        var rng = world.Rng;
        var ladder = world.Escalation!;
        var countries = world.Countries.All;
        Country a;
        Country b;
        string subject;
        string cause;
        var boost = 0.0;
        var roll = rng.NextDouble();
        var hot = ladder.Fronts.Where(f => f.Rung >= Rung.Incident).ToList();
        if (roll < 0.10 && doctrine.Summits.Count > 0)
        {
            var tie = Pick(rng, doctrine.Summits);
            a = world.Countries.Get(tie.First);
            b = world.Countries.Get(tie.Second);
            subject = tie.Subject;
            cause = tie.Reason;
            boost = 0.2;
        }
        else if (roll < 0.35 && hot.Count > 0)
        {
            var state = Pick(rng, hot);
            a = world.Countries.Get(state.Front.First);
            b = world.Countries.Get(state.Front.Second);
            subject = $"ceasefire on the {state.Front.Name}";
            cause = $"the {state.Front.Name} has reached {Lower(state.Rung)} level";
            boost = 0.25;
        }
        else if (roll < 0.65 && doctrine.Aid.Count > 0)
        {
            var tie = Pick(rng, doctrine.Aid);
            a = world.Countries.Get(tie.Patron);
            b = world.Countries.Get(tie.Client);
            subject = "aid agreement";
            cause = tie.Reason;
        }
        else
        {
            a = Pick(rng, countries);
            var others = countries
                .Where(c => c.Id != a.Id && world.Relations.Get(a.Id, c.Id) > -0.2)
                .ToList();
            if (others.Count == 0)
            {
                return;
            }
            b = Pick(rng, others);
            subject = Subject(rng, a, b);
            cause = "routine diplomatic contact";
        }
        if (Busy(world, InteractionKind.Negotiation, a.Id, b.Id))
        {
            return;
        }
        var chance = Math.Clamp(0.55 + world.Relations.Get(a.Id, b.Id) * 0.35 + boost, 0.1, 0.9);
        var duration = rng.Next(12, 30);
        var succeeds = rng.NextDouble() < chance;
        var resolve = tick + (succeeds ? duration : Math.Max(3, (int)(duration * (0.3 + rng.NextDouble() * 0.5))));
        world.Interactions.Open(
            InteractionKind.Negotiation, a.Id, b.Id, TransportMode.None, 0, subject, tick, tick + duration, resolve,
            succeeds ? InteractionStatus.Succeeded : InteractionStatus.Cancelled, null, cause);
        Log(world, "negotiation_started", $"{a.Name} opens talks with {b.Name}: {subject}, because {cause}");
    }

    private static TransportMode ChooseMode(Random rng, Country a, Country b)
    {
        if (a.Landmass.Length > 0 && a.Landmass == b.Landmass && rng.NextDouble() < 0.8)
        {
            return TransportMode.Ground;
        }
        return rng.NextDouble() < 0.65 ? TransportMode.Naval : TransportMode.Air;
    }

    // A zone is hostile to a shipment when its owner is hostile to the sender or to the receiver.
    private static bool ZoneHostile(WorldState world, CountryId zone, CountryId sender, CountryId receiver) =>
        world.Relations.Get(sender, zone) < RouteNetwork.HostileThreshold
        || world.Relations.Get(receiver, zone) < RouteNetwork.HostileThreshold;

    private void Ship(WorldState world, long tick)
    {
        if (world.Doctrine is { } doctrine)
        {
            ShipByDoctrine(world, tick, doctrine);
            return;
        }
        var rng = world.Rng;
        var countries = world.Countries.All;
        var senders = countries.Where(c => c.Supplies >= 120).ToList();
        if (senders.Count == 0)
        {
            return;
        }
        var a = Pick(rng, senders);
        var receivers = countries
            .Where(c => c.Id != a.Id
                && world.Relations.Get(a.Id, c.Id) > 0.15
                && !Busy(world, InteractionKind.SupplyShipment, a.Id, c.Id))
            .ToList();
        if (receivers.Count == 0)
        {
            return;
        }
        var b = Pick(rng, receivers);
        var cargo = Math.Round(Math.Min(a.Supplies * 0.15, 40 + rng.NextDouble() * 60));
        var mode = ChooseMode(rng, a, b);
        ShipmentRoute? route = null;
        if (world.Routes is { } network)
        {
            var planned = network.PlanBest(
                mode,
                a,
                b,
                countries,
                id => world.Relations.Get(a.Id, id) < RouteNetwork.HostileThreshold
                    || world.Relations.Get(b.Id, id) < RouteNetwork.HostileThreshold);
            if (planned is null)
            {
                return;
            }
            mode = planned.Value.Mode;
            route = planned.Value.Route;
        }
        var distance = Vector2.Distance(a.Capital, b.Capital);
        var speed = mode switch
        {
            TransportMode.Ground => 25.0,
            TransportMode.Naval => 18.0,
            _ => 70.0
        };
        var duration = route is null
            ? (int)Math.Clamp(distance / speed + 6, 8, 70)
            : (int)Math.Clamp(Math.Round(route.TotalTicks) + 6, 8, 160);
        var risk = 0.08 + (mode == TransportMode.Naval ? 0.10 : 0) + (mode == TransportMode.Air ? 0.05 : 0)
            + (route is null ? 0 : Math.Min(0.1, route.Exposure * 0.002));
        var intercepted = rng.NextDouble() < risk;
        var resolve = tick + (intercepted ? Math.Max(2, (int)(duration * (0.2 + rng.NextDouble() * 0.7))) : duration);
        a.AddSupplies(-cargo);
        world.Interactions.Open(
            InteractionKind.SupplyShipment, a.Id, b.Id, mode, cargo, "supplies", tick, tick + duration, resolve,
            intercepted ? InteractionStatus.Intercepted : InteractionStatus.Succeeded, route);
        Log(world, "supply_dispatched", $"{a.Name} sends {cargo:0} supplies to {b.Name} by {mode.ToString().ToLowerInvariant()}");
    }

    private void ShipByDoctrine(WorldState world, long tick, Doctrine doctrine)
    {
        var rng = world.Rng;
        var countries = world.Countries.All;
        var aidOptions = new List<(Country Giver, Country Taker, string Reason)>();
        var tradeOptions = new List<(Country Giver, Country Taker, string Reason)>();
        foreach (var tie in doctrine.Aid)
        {
            var giver = world.Countries.Get(tie.Patron);
            var taker = world.Countries.Get(tie.Client);
            if (giver.Supplies >= 120
                && taker.Supplies < giver.Supplies
                && world.Relations.Get(giver.Id, taker.Id) > 0.0
                && world.Zones.Severity(giver.Id, tick) < ShippingBlockSeverity
                && !Busy(world, InteractionKind.SupplyShipment, giver.Id, taker.Id))
            {
                aidOptions.Add((giver, taker, tie.Reason));
            }
        }
        foreach (var giver in countries.Where(c => c.Supplies >= 120
                     && c.Bloc != Bloc.NonAligned
                     && world.Zones.Severity(c.Id, tick) < ShippingBlockSeverity))
        {
            foreach (var taker in countries)
            {
                if (taker.Id != giver.Id
                    && taker.Bloc == giver.Bloc
                    && taker.Supplies < giver.Supplies
                    && world.Relations.Get(giver.Id, taker.Id) > 0.4
                    && doctrine.AidFor(giver.Id, taker.Id) is null
                    && !Busy(world, InteractionKind.SupplyShipment, giver.Id, taker.Id))
                {
                    tradeOptions.Add((giver, taker, "bloc partners keep their trade moving"));
                }
            }
        }
        var useAid = aidOptions.Count > 0 && (tradeOptions.Count == 0 || rng.NextDouble() < 0.7);
        var pool = useAid ? aidOptions : tradeOptions;
        if (pool.Count == 0)
        {
            return;
        }
        var (a, b, reason) = Pick(rng, pool);
        var cargo = Math.Round(Math.Min(a.Supplies * 0.15, 40 + rng.NextDouble() * 60));
        var mode = ChooseMode(rng, a, b);
        ShipmentRoute? route = null;
        var gauntlet = false;
        if (world.Routes is { } network)
        {
            bool Hostile(CountryId id) => ZoneHostile(world, id, a.Id, b.Id);
            var planned = network.PlanBest(mode, a, b, countries, Hostile);
            if (planned is null)
            {
                if (!useAid || rng.NextDouble() >= GauntletChance)
                {
                    return;
                }
                planned = network.PlanBest(mode, a, b, countries, _ => false);
                if (planned is null)
                {
                    return;
                }
                gauntlet = true;
            }
            mode = planned.Value.Mode;
            route = planned.Value.Route;
        }
        var distance = Vector2.Distance(a.Capital, b.Capital);
        var speed = mode switch
        {
            TransportMode.Ground => 25.0,
            TransportMode.Naval => 18.0,
            _ => 70.0
        };
        var duration = route is null
            ? (int)Math.Clamp(distance / speed + 6, 8, 70)
            : (int)Math.Clamp(Math.Round(route.TotalTicks) + 6, 8, 160);
        a.AddSupplies(-cargo);
        var cause = gauntlet ? $"{reason}; no safe lane exists, so the convoy runs through hostile territory" : reason;
        world.Interactions.Open(
            InteractionKind.SupplyShipment, a.Id, b.Id, mode, cargo, "supplies", tick, tick + duration, tick + duration,
            InteractionStatus.Succeeded, route, cause);
        Log(
            world,
            "supply_dispatched",
            $"{a.Name} sends {cargo:0} supplies to {b.Name} by {mode.ToString().ToLowerInvariant()}, because {cause}");
    }

    private static IEnumerable<UnitKind> InterceptorKinds(UnitKind carrier) => carrier switch
    {
        UnitKind.CargoShip => new[] { UnitKind.Warship, UnitKind.Naval },
        UnitKind.Truck => new[] { UnitKind.Tank, UnitKind.Armor, UnitKind.ArmoredCar },
        _ => new[] { UnitKind.Warplane, UnitKind.Air, UnitKind.Helicopter }
    };

    private static Unit? FindInterceptor(WorldState world, CountryId hunter, UnitKind carrier, Vector2 at)
    {
        var kinds = InterceptorKinds(carrier).ToHashSet();
        return world.Units
            .Where(u => u.Owner == hunter && u.Status != UnitStatus.Destroyed && kinds.Contains(u.Kind))
            .OrderBy(u => Vector2.DistanceSquared(u.Position, at))
            .ThenBy(u => u.Id)
            .FirstOrDefault();
    }

    // A hostile country tracks a shipment that enters its land or waters and, while it stays inside,
    // gets a chance each tick to catch it with an interceptor of the right domain.
    private void Hunt(WorldState world, long tick)
    {
        if (world.Routes is not { } network)
        {
            return;
        }
        foreach (var shipment in world.Interactions.Active
                     .Where(i => i.Kind == InteractionKind.SupplyShipment && i.Route is not null && i.ResolveTick > tick)
                     .ToList())
        {
            var position = shipment.Route!.At(shipment.ProgressAt(tick));
            var zone = network.ZoneAt(position.Point);
            if (zone is null || zone == shipment.Source || zone == shipment.Target)
            {
                continue;
            }
            var hunterId = zone.Value;
            if (!ZoneHostile(world, hunterId, shipment.Source, shipment.Target))
            {
                continue;
            }
            var hunter = world.Countries.Get(hunterId);
            var interceptor = FindInterceptor(world, hunterId, position.Carrier, position.Point);
            if (shipment.Tracker != zone)
            {
                shipment.Track(hunterId);
                var sender = world.Countries.Get(shipment.Source);
                var receiver = world.Countries.Get(shipment.Target);
                Log(
                    world,
                    "shipment_tracked",
                    $"{hunter.Name} is tracking the {sender.Name} shipment to {receiver.Name} inside its territory, because relations are hostile");
                if (interceptor is not null)
                {
                    world.TryOrderMove(interceptor, position.Point);
                }
            }
            if (interceptor is null)
            {
                continue;
            }
            var hostility = Math.Clamp(
                Math.Max(
                    -world.Relations.Get(hunterId, shipment.Source),
                    -world.Relations.Get(hunterId, shipment.Target)),
                0.0,
                1.0);
            if (world.Rng.NextDouble() < HuntBase + HuntHostility * hostility)
            {
                shipment.Intercept(hunterId, tick);
            }
        }
    }

    private void Clash(WorldState world, long tick)
    {
        if (world.Doctrine is not null)
        {
            ClashByDoctrine(world, tick);
            return;
        }
        var rng = world.Rng;
        var all = world.Countries.All;
        var pairs = new List<(Country, Country)>();
        for (var i = 0; i < all.Count; i++)
        {
            for (var j = i + 1; j < all.Count; j++)
            {
                var a = all[i];
                var b = all[j];
                if (world.Relations.Get(a.Id, b.Id) < ClashThreshold
                    && !Forbidden(world, a.Id, b.Id)
                    && !Busy(world, InteractionKind.Clash, a.Id, b.Id))
                {
                    pairs.Add((a, b));
                }
            }
        }
        if (pairs.Count == 0)
        {
            return;
        }
        var (first, second) = Pick(rng, pairs);
        if (rng.NextDouble() < 0.5)
        {
            (first, second) = (second, first);
        }
        var duration = rng.Next(6, 15);
        world.Interactions.Open(
            InteractionKind.Clash, first.Id, second.Id, TransportMode.None, 0, "border engagement",
            tick, tick + duration, tick + duration, InteractionStatus.Resolved);
        Log(world, "clash_started", $"{first.Name} and {second.Name} forces are engaged");
    }

    private void ClashByDoctrine(WorldState world, long tick)
    {
        var rng = world.Rng;
        var pairs = new List<(FrontState Front, Country First, Country Second)>();
        foreach (var state in world.Escalation!.Fronts)
        {
            var a = world.Countries.Get(state.Front.First);
            var b = world.Countries.Get(state.Front.Second);
            if (state.Rung >= Rung.Clash
                && !Forbidden(world, a.Id, b.Id)
                && !Busy(world, InteractionKind.Clash, a.Id, b.Id))
            {
                pairs.Add((state, a, b));
            }
        }
        if (pairs.Count == 0)
        {
            return;
        }
        var (front, first, second) = Pick(rng, pairs);
        if (rng.NextDouble() < 0.5)
        {
            (first, second) = (second, first);
        }
        var duration = rng.Next(6, 15);
        var cause = $"the {front.Front.Name} has escalated to open fighting";
        world.Interactions.Open(
            InteractionKind.Clash, first.Id, second.Id, TransportMode.None, 0, "border engagement",
            tick, tick + duration, tick + duration, InteractionStatus.Resolved, null, cause);
        Log(world, "clash_started", $"{first.Name} and {second.Name} forces are engaged, because {cause}");
    }

    private void Missile(WorldState world, long tick)
    {
        if (world.Doctrine is not null)
        {
            MissileByDoctrine(world, tick);
            return;
        }
        var rng = world.Rng;
        var all = world.Countries.All;
        var pairs = new List<(Country, Country)>();
        foreach (var a in all)
        {
            foreach (var b in all)
            {
                if (a.Id != b.Id
                    && a.Ammunition >= 40
                    && world.Relations.Get(a.Id, b.Id) < MissileThreshold
                    && !Forbidden(world, a.Id, b.Id)
                    && !Busy(world, InteractionKind.MissileStrike, a.Id, b.Id))
                {
                    pairs.Add((a, b));
                }
            }
        }
        if (pairs.Count == 0)
        {
            return;
        }
        var (source, target) = Pick(rng, pairs);
        LaunchMissile(world, tick, source, target, "");
    }

    private void MissileByDoctrine(WorldState world, long tick)
    {
        var pairs = new List<(Country Source, Country Target, FrontState Front)>();
        foreach (var state in world.Escalation!.Fronts)
        {
            if (state.Rung < Rung.Missile)
            {
                continue;
            }
            var a = world.Countries.Get(state.Front.First);
            var b = world.Countries.Get(state.Front.Second);
            if (Forbidden(world, a.Id, b.Id))
            {
                continue;
            }
            foreach (var (source, target) in new[] { (a, b), (b, a) })
            {
                if (source.Ammunition >= 40 && !Busy(world, InteractionKind.MissileStrike, source.Id, target.Id))
                {
                    pairs.Add((source, target, state));
                }
            }
        }
        if (pairs.Count == 0)
        {
            return;
        }
        var (origin, aim, front) = Pick(world.Rng, pairs);
        LaunchMissile(world, tick, origin, aim, $"the {front.Front.Name} has reached missile level");
    }

    private void LaunchMissile(WorldState world, long tick, Country source, Country target, string cause)
    {
        var rng = world.Rng;
        var hits = rng.NextDouble() < 0.7;
        var distance = Vector2.Distance(source.Capital, target.Capital);
        var duration = (int)Math.Clamp(5 + distance / 60, 5, 28);
        var resolve = tick + (hits ? duration : Math.Max(2, (int)(duration * (0.5 + rng.NextDouble() * 0.45))));
        source.AddAmmunition(-20);
        world.Interactions.Open(
            InteractionKind.MissileStrike, source.Id, target.Id, TransportMode.Air, 0, "ballistic strike",
            tick, tick + duration, resolve, hits ? InteractionStatus.Succeeded : InteractionStatus.Intercepted, null, cause);
        Log(
            world,
            "missile_launched",
            cause.Length == 0
                ? $"{source.Name} launches a missile at {target.Name}"
                : $"{source.Name} launches a missile at {target.Name}, because {cause}");
    }

    // A missile that lands leaves a paralyzed patch around the capital. A big stockpile can go up with it,
    // which adds a second, larger blast on top of the first.
    private static void Devastate(WorldState world, Country victim, long tick)
    {
        var rng = world.Rng;
        var angle = rng.NextDouble() * Math.PI * 2;
        var distance = rng.NextDouble() * 10;
        var center = victim.Capital + new Vector2((float)(Math.Cos(angle) * distance), (float)(Math.Sin(angle) * distance));
        var radius = 10f + (float)(rng.NextDouble() * 8);
        var severity = 0.6 + rng.NextDouble() * 0.4;
        var repair = tick + rng.Next(80, 160);
        var stock = victim.Supplies;
        var secondary = stock >= 150 && rng.NextDouble() < Math.Clamp(stock / 600.0, 0.1, 0.8);
        if (secondary)
        {
            radius *= 1.6f;
            severity = Math.Min(1.0, severity + 0.2);
            var lost = Math.Round(victim.Supplies * 0.25);
            victim.AddSupplies(-lost);
            var nearest = world.Units
                .Where(u => u.Owner == victim.Id && u.Status != UnitStatus.Destroyed)
                .OrderBy(u => Vector2.DistanceSquared(u.Position, center))
                .ThenBy(u => u.Id)
                .Take(2)
                .ToList();
            foreach (var unit in nearest)
            {
                unit.ApplyDamage(25 + rng.NextDouble() * 20);
            }
            Log(
                world,
                "secondary_explosion",
                $"the stockpile at {victim.Name} detonated, adding a larger blast and destroying {lost:0} supplies");
        }
        world.Zones.Add(victim.Id, center, radius, severity, tick, repair, secondary ? "strike and secondary explosion" : "missile strike");
        Log(world, "zone_paralyzed", $"part of {victim.Name} is paralyzed until it is repaired");
    }

    private static void Damage(WorldState world, CountryId owner, double amount)
    {
        var candidates = world.Units
            .Where(u => u.Owner == owner && u.Status != UnitStatus.Destroyed)
            .ToList();
        if (candidates.Count > 0)
        {
            Pick(world.Rng, candidates).ApplyDamage(amount);
        }
    }

    private void Resolve(WorldState world, Interaction interaction, long tick)
    {
        interaction.Complete(tick);
        var rng = world.Rng;
        var a = world.Countries.Get(interaction.Source);
        var b = world.Countries.Get(interaction.Target);
        var succeeded = interaction.Status == InteractionStatus.Succeeded;
        switch (interaction.Kind)
        {
            case InteractionKind.Negotiation:
                world.Relations.Adjust(a.Id, b.Id, succeeded ? 0.12 : -0.15);
                Log(
                    world,
                    succeeded ? "negotiation_concluded" : "negotiation_cancelled",
                    succeeded
                        ? $"{a.Name} and {b.Name} concluded talks: {interaction.Subject}"
                        : $"talks between {a.Name} and {b.Name} collapsed: {interaction.Subject}");
                if (succeeded
                    && world.Escalation is { } ladder
                    && interaction.Subject.StartsWith("ceasefire", StringComparison.Ordinal)
                    && ladder.Cool(a.Id, b.Id, tick) is { } move)
                {
                    ApplyMove(world, move, "ceasefire talks succeeded");
                }
                break;
            case InteractionKind.SupplyShipment:
                if (succeeded)
                {
                    b.AddSupplies(interaction.Cargo);
                    Log(world, "supply_delivered", $"{interaction.Cargo:0} supplies from {a.Name} reached {b.Name}");
                }
                else if (interaction.InterceptedBy is { } by)
                {
                    var hunter = world.Countries.Get(by);
                    var seized = Math.Round(interaction.Cargo * 0.4);
                    hunter.AddSupplies(seized);
                    world.Relations.Adjust(a.Id, hunter.Id, -0.06);
                    Log(
                        world,
                        "supply_intercepted",
                        $"{hunter.Name} intercepted the shipment from {a.Name} to {b.Name} as it crossed hostile territory, and seized {seized:0} supplies");
                }
                else
                {
                    Log(world, "supply_intercepted", $"shipment from {a.Name} to {b.Name} was lost en route");
                }
                break;
            case InteractionKind.Clash:
                Damage(world, a.Id, 10 + rng.NextDouble() * 20);
                Damage(world, b.Id, 10 + rng.NextDouble() * 20);
                a.AddAmmunition(-(10 + rng.NextDouble() * 15));
                b.AddAmmunition(-(10 + rng.NextDouble() * 15));
                world.Relations.Adjust(a.Id, b.Id, -0.08);
                Log(world, "clash_ended", $"engagement between {a.Name} and {b.Name} ended with losses on both sides");
                break;
            case InteractionKind.MissileStrike:
                if (succeeded)
                {
                    b.AddSupplies(-(40 + rng.NextDouble() * 50));
                    Damage(world, b.Id, 20 + rng.NextDouble() * 30);
                    world.Relations.Adjust(a.Id, b.Id, -0.25);
                    Log(world, "missile_impact", $"missile from {a.Name} struck {b.Name}");
                    if (world.Doctrine is not null)
                    {
                        Devastate(world, b, tick);
                    }
                }
                else
                {
                    world.Relations.Adjust(a.Id, b.Id, -0.1);
                    Log(world, "missile_intercepted", $"missile from {a.Name} was intercepted before reaching {b.Name}");
                }
                break;
        }
    }
}
