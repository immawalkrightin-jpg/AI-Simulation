using System.Numerics;
using WorldEngine.Countries;
using WorldEngine.Diplomacy;
using WorldEngine.Events;
using WorldEngine.SimulationClock;
using WorldEngine.Units;
using WorldEngine.World;
using Xunit;

namespace Simulation.Tests;

public class WorldEngineTests
{
    private static WorldState BuildWorld(int seed)
    {
        var world = new WorldState(seed, 1000, 1000);
        world.Countries.Add(new Country(new CountryId("USA"), "United States", new Vector2(100, 100), 500, 500));
        world.Countries.Add(new Country(new CountryId("ITA"), "Italy", new Vector2(800, 200), 300, 300));
        world.Countries.Add(new Country(new CountryId("JPN"), "Japan", new Vector2(900, 800), 400, 400));
        world.AddUnit(new CountryId("USA"), UnitKind.Armor, new Vector2(100, 100), 5, 100);
        world.AddUnit(new CountryId("ITA"), UnitKind.Naval, new Vector2(800, 200), 8, 100);
        world.AddUnit(new CountryId("JPN"), UnitKind.SupplyConvoy, new Vector2(900, 800), 6, 50);
        return world;
    }

    [Fact]
    public void ClockAdvancesByTickSeconds()
    {
        var clock = new SimClock(30);
        clock.Advance();
        clock.Advance();
        Assert.Equal(2, clock.Tick);
        Assert.Equal(60, clock.ElapsedSeconds);
    }

    [Fact]
    public void ClockRejectsNonPositiveTickLength()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SimClock(0));
    }

    [Fact]
    public void RelationsAreSymmetricAndClamped()
    {
        var matrix = new RelationMatrix();
        var a = new CountryId("USA");
        var b = new CountryId("ITA");
        matrix.Adjust(a, b, -5);
        Assert.Equal(-1, matrix.Get(b, a));
        Assert.Equal(RelationState.Hostile, matrix.StateOf(a, b));
    }

    [Fact]
    public void UnitReachesDestinationAndGoesIdle()
    {
        var world = BuildWorld(1);
        var unit = world.GetUnit(1);
        unit.OrderMove(new Vector2(100, 130));
        for (var i = 0; i < 10; i++)
        {
            unit.Step(1);
        }
        Assert.Equal(new Vector2(100, 130), unit.Position);
        Assert.Equal(UnitStatus.Idle, unit.Status);
    }

    [Fact]
    public void DestroyedUnitIgnoresOrders()
    {
        var world = BuildWorld(1);
        var unit = world.GetUnit(1);
        unit.ApplyDamage(1000);
        unit.OrderMove(new Vector2(0, 0));
        Assert.Equal(UnitStatus.Destroyed, unit.Status);
        Assert.Null(unit.Destination);
    }

    [Fact]
    public void SchedulerReturnsEventsInTickOrder()
    {
        var scheduler = new EventScheduler();
        var a = new CountryId("USA");
        var b = new CountryId("ITA");
        scheduler.Schedule(new RelationShiftEvent(a, b, 0.1, "late") { ScheduledTick = 5 });
        scheduler.Schedule(new RelationShiftEvent(a, b, 0.1, "early") { ScheduledTick = 2 });
        var due = scheduler.TakeDue(3);
        Assert.Single(due);
        Assert.Equal(1, scheduler.Pending);
    }

    [Fact]
    public void WorldProducesActivityWithoutExternalInput()
    {
        var simulation = new WorldSimulation(BuildWorld(42));
        simulation.Run(200);
        Assert.NotEmpty(simulation.State.Log.Records);
    }

    [Fact]
    public void SameSeedProducesIdenticalHistory()
    {
        var first = new WorldSimulation(BuildWorld(7));
        var second = new WorldSimulation(BuildWorld(7));
        first.Run(300);
        second.Run(300);
        Assert.Equal(first.State.Log.Records, second.State.Log.Records);
    }

    [Fact]
    public void DifferentSeedsDiverge()
    {
        var first = new WorldSimulation(BuildWorld(7));
        var second = new WorldSimulation(BuildWorld(8));
        first.Run(300);
        second.Run(300);
        Assert.NotEqual(first.State.Log.Records, second.State.Log.Records);
    }
}
