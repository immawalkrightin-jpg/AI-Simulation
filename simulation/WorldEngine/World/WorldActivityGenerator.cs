using WorldEngine.Events;
using WorldEngine.Units;

namespace WorldEngine.World;

public sealed class WorldActivityGenerator
{
    private readonly int interval;
    private readonly double probability;

    public WorldActivityGenerator(int interval = 5, double probability = 0.4)
    {
        if (interval <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(interval));
        }
        this.interval = interval;
        this.probability = probability;
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
            var owned = world.Units
                .Where(u => u.Owner == country.Id && u.Status != UnitStatus.Destroyed)
                .ToList();
            if (owned.Count == 0)
            {
                continue;
            }
            var unit = owned[world.Rng.Next(owned.Count)];
            world.Scheduler.Schedule(
                new UnitDispatchEvent(unit.Id, world.RandomPoint()) { ScheduledTick = due });
        }
    }
}
