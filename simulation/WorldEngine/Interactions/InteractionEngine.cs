using System.Numerics;
using WorldEngine.Countries;
using WorldEngine.Units;
using WorldEngine.World;

namespace WorldEngine.Interactions;

public sealed class InteractionEngine
{
    public const int FadeTicks = 8;
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
                country.AddSupplies(8);
                country.AddAmmunition(3);
            }
        }
        Spawn(world, tick);
        world.Interactions.Prune(tick, FadeTicks * 2);
    }

    private static (string, string) Key(CountryId a, CountryId b) =>
        string.CompareOrdinal(a.Value, b.Value) <= 0 ? (a.Value, b.Value) : (b.Value, a.Value);

    private static T Pick<T>(Random rng, IReadOnlyList<T> list) => list[rng.Next(list.Count)];

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

    private static TransportMode ChooseMode(Random rng, Country a, Country b)
    {
        if (a.Landmass.Length > 0 && a.Landmass == b.Landmass && rng.NextDouble() < 0.8)
        {
            return TransportMode.Ground;
        }
        return rng.NextDouble() < 0.65 ? TransportMode.Naval : TransportMode.Air;
    }

    private void Ship(WorldState world, long tick)
    {
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
        var distance = Vector2.Distance(a.Capital, b.Capital);
        var speed = mode switch
        {
            TransportMode.Ground => 25.0,
            TransportMode.Naval => 18.0,
            _ => 70.0
        };
        var duration = (int)Math.Clamp(distance / speed + 6, 8, 70);
        var risk = 0.08 + (mode == TransportMode.Naval ? 0.10 : 0) + (mode == TransportMode.Air ? 0.05 : 0);
        var intercepted = rng.NextDouble() < risk;
        var resolve = tick + (intercepted ? Math.Max(2, (int)(duration * (0.2 + rng.NextDouble() * 0.7))) : duration);
        a.AddSupplies(-cargo);
        world.Interactions.Open(
            InteractionKind.SupplyShipment, a.Id, b.Id, mode, cargo, "supplies", tick, tick + duration, resolve,
            intercepted ? InteractionStatus.Intercepted : InteractionStatus.Succeeded);
        Log(world, "supply_dispatched", $"{a.Name} sends {cargo:0} supplies to {b.Name} by {mode.ToString().ToLowerInvariant()}");
    }

    private void Clash(WorldState world, long tick)
    {
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
                    && !noDirect.Contains(Key(a.Id, b.Id))
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

    private void Missile(WorldState world, long tick)
    {
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
                    && !noDirect.Contains(Key(a.Id, b.Id))
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
        var hits = rng.NextDouble() < 0.7;
        var distance = Vector2.Distance(source.Capital, target.Capital);
        var duration = (int)Math.Clamp(5 + distance / 60, 5, 28);
        var resolve = tick + (hits ? duration : Math.Max(2, (int)(duration * (0.5 + rng.NextDouble() * 0.45))));
        source.AddAmmunition(-20);
        world.Interactions.Open(
            InteractionKind.MissileStrike, source.Id, target.Id, TransportMode.Air, 0, "ballistic strike",
            tick, tick + duration, resolve, hits ? InteractionStatus.Succeeded : InteractionStatus.Intercepted);
        Log(world, "missile_launched", $"{source.Name} launches a missile at {target.Name}");
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
                break;
            case InteractionKind.SupplyShipment:
                if (succeeded)
                {
                    b.AddSupplies(interaction.Cargo);
                    Log(world, "supply_delivered", $"{interaction.Cargo:0} supplies from {a.Name} reached {b.Name}");
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
