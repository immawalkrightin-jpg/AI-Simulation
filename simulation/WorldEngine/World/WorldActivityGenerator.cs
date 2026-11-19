using System.Numerics;
using WorldEngine.Events;
using WorldEngine.Units;

namespace WorldEngine.World;

public sealed class WorldActivityGenerator
{
    private readonly int interval;
    private readonly double probability;
    private readonly bool dispatchUnits;
    private readonly float patrolRadius;

    public WorldActivityGenerator(
        int interval = 5,
        double probability = 0.4,
        bool dispatchUnits = true,
        float patrolRadius = 0)
    {
        if (interval <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(interval));
        }
        this.interval = interval;
        this.probability = probability;
        this.dispatchUnits = dispatchUnits;
        this.patrolRadius = patrolRadius;
    }

    public void Step(WorldState world)
    {
        if (world.Clock.Tick % interval != 0)
        {
            return;
        }
        var countries = world.Countries.All;
        foreach (var country in countries)
        {
            if (world.Rng.NextDouble() > probability)
            {
                continue;
            }
            var due = world.Clock.Tick + world.Rng.Next(1, 5);
            if (world.Rng.NextDouble() < 0.5 && countries.Count > 1)
            {
                var others = countries.Where(c => c.Id != country.Id).ToList();
                var other = others[world.Rng.Next(others.Count)];
                var delta = (world.Rng.NextDouble() - 0.5) * 0.2;
                var reason = delta < 0 ? "diplomatic friction" : "diplomatic engagement";
                world.Scheduler.Schedule(
                    new RelationShiftEvent(country.Id, other.Id, delta, reason) { ScheduledTick = due });
                continue;
            }
            if (!dispatchUnits)
            {
                continue;
            }
            var owned = world.Units
                .Where(u => u.Owner == country.Id && u.Status != UnitStatus.Destroyed)
                .ToList();
            if (owned.Count == 0)
            {
                continue;
            }
            var unit = owned[world.Rng.Next(owned.Count)];
            var destination = patrolRadius > 0 ? Patrol(world, unit) : world.RandomPoint();
            world.Scheduler.Schedule(new UnitDispatchEvent(unit.Id, destination) { ScheduledTick = due });
        }
    }

    private Vector2 Patrol(WorldState world, Unit unit)
    {
        var angle = world.Rng.NextDouble() * Math.PI * 2;
        var radius = world.Rng.NextDouble() * patrolRadius;
        var point = unit.Position + new Vector2((float)(Math.Cos(angle) * radius), (float)(Math.Sin(angle) * radius));
        return new Vector2(Math.Clamp(point.X, 0, world.Width), Math.Clamp(point.Y, 0, world.Height));
    }
}
