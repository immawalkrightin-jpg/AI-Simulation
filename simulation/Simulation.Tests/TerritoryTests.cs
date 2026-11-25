using System.Numerics;
using Simulation.API.Scenarios;
using WorldEngine.Countries;
using WorldEngine.Events;
using WorldEngine.Territory;
using WorldEngine.Units;
using WorldEngine.World;
using Xunit;

namespace Simulation.Tests;

public class TerritoryTests
{
    private const string MapJson =
        "{\"width\":20,\"height\":10,\"entities\":["
        + "{\"id\":\"AAA\",\"rings\":[[0,0,8,0,8,10,0,10]]},"
        + "{\"id\":\"BBB\",\"rings\":[[12,0,20,0,20,10,12,10]]},"
        + "{\"id\":\"\",\"rings\":[[8,0,12,0,12,4,8,4]]}"
        + "]}";

    private const string CheckerJson =
        "{\"width\":4,\"height\":4,\"entities\":["
        + "{\"id\":\"AAA\",\"rings\":[[0,0,2,0,2,2,0,2],[2,2,4,2,4,4,2,4]]}"
        + "]}";

    private static readonly CountryId A = new("AAA");
    private static readonly CountryId B = new("BBB");

    private static TerritoryMap Map() => TerritoryLoader.FromJson(MapJson);

    [Fact]
    public void RasterReadsOwnersSeaAndFreeLand()
    {
        var map = Map();
        Assert.Equal(10, map.Columns);
        Assert.Equal(5, map.Rows);
        Assert.Equal("AAA", map.OwnerAt(new Vector2(4, 5))?.Value);
        Assert.Equal("BBB", map.OwnerAt(new Vector2(16, 5))?.Value);
        Assert.Null(map.OwnerAt(new Vector2(10, 7)));
        Assert.True(map.IsSea(new Vector2(10, 7)));
        Assert.True(map.IsLand(new Vector2(10, 2)));
        Assert.Null(map.OwnerAt(new Vector2(10, 2)));
        Assert.False(map.IsSea(new Vector2(4, 5)));
    }

    [Fact]
    public void PointsOutsideTheMapAreNeverPermitted()
    {
        var map = Map();
        Assert.False(map.Contains(new Vector2(-1, 5)));
        Assert.False(map.Contains(new Vector2(20, 5)));
        Assert.False(map.Permits(Domain.Air, A, new Vector2(25, 5)));
        Assert.False(map.SegmentPermits(Domain.Air, A, new Vector2(4, 5), new Vector2(40, 5)));
    }

    [Fact]
    public void LandUnitsAreConfinedToTheirOwnLand()
    {
        var map = Map();
        Assert.True(map.Permits(Domain.Land, A, new Vector2(4, 5)));
        Assert.False(map.Permits(Domain.Land, A, new Vector2(16, 5)));
        Assert.False(map.Permits(Domain.Land, A, new Vector2(10, 7)));
        Assert.False(map.Permits(Domain.Land, A, new Vector2(10, 2)));
    }

    [Fact]
    public void NavalUnitsAreConfinedToWater()
    {
        var map = Map();
        Assert.True(map.Permits(Domain.Sea, A, new Vector2(10, 7)));
        Assert.False(map.Permits(Domain.Sea, A, new Vector2(4, 5)));
        Assert.False(map.Permits(Domain.Sea, A, new Vector2(10, 2)));
    }

    [Fact]
    public void AirUnitsMayNotOverflyForeignLand()
    {
        var map = Map();
        Assert.True(map.Permits(Domain.Air, A, new Vector2(4, 5)));
        Assert.True(map.Permits(Domain.Air, A, new Vector2(10, 7)));
        Assert.True(map.Permits(Domain.Air, A, new Vector2(10, 2)));
        Assert.False(map.Permits(Domain.Air, A, new Vector2(16, 5)));
        Assert.True(map.Permits(Domain.Air, B, new Vector2(16, 5)));
    }

    [Fact]
    public void SegmentsAreRejectedWhenAnyCellOnThePathIsIllegal()
    {
        var map = Map();
        Assert.True(map.SegmentPermits(Domain.Land, A, new Vector2(1, 5), new Vector2(7, 5)));
        Assert.False(map.SegmentPermits(Domain.Land, A, new Vector2(1, 5), new Vector2(15, 5)));
        Assert.True(map.SegmentPermits(Domain.Sea, A, new Vector2(9, 7), new Vector2(11, 9)));
        Assert.False(map.SegmentPermits(Domain.Sea, A, new Vector2(9, 7), new Vector2(9, 1)));
        Assert.True(map.SegmentPermits(Domain.Land, A, new Vector2(3, 3), new Vector2(3, 3)));
    }

    [Fact]
    public void DiagonalSegmentsCannotCutThroughACornerBetweenTwoLegalCells()
    {
        var map = TerritoryLoader.FromJson(CheckerJson);
        Assert.True(map.Permits(Domain.Land, A, new Vector2(1, 1)));
        Assert.True(map.Permits(Domain.Land, A, new Vector2(3, 3)));
        Assert.False(map.Permits(Domain.Land, A, new Vector2(3, 1)));
        Assert.False(map.SegmentPermits(Domain.Land, A, new Vector2(1.9f, 1.8f), new Vector2(2.1f, 2.1f)));
        Assert.True(map.SegmentPermits(Domain.Land, A, new Vector2(1f, 1f), new Vector2(1.5f, 1.5f)));
    }

    [Fact]
    public void FindNearestReturnsTheClosestPermittedCell()
    {
        var map = Map();
        var found = map.FindNearest(Domain.Land, A, new Vector2(10, 7), 10);
        Assert.NotNull(found);
        Assert.True(map.Permits(Domain.Land, A, found!.Value));
        Assert.True(Vector2.Distance(found.Value, new Vector2(10, 7)) <= 3.01f);
        Assert.Null(map.FindNearest(Domain.Land, A, new Vector2(10, 7), 1));
    }

    [Fact]
    public void WorldRefusesOrdersThatLeaveTheUnitsTerrain()
    {
        var world = new WorldState(1, 20, 10) { Territory = Map() };
        world.Countries.Add(new Country(A, "A", new Vector2(4, 5), 100, 100));
        var tank = world.AddUnit(A, UnitKind.Armor, new Vector2(4, 5), 1, 100);
        Assert.False(world.TryOrderMove(tank, new Vector2(16, 5)));
        Assert.Equal(UnitStatus.Idle, tank.Status);
        Assert.True(world.TryOrderMove(tank, new Vector2(6, 5)));
        Assert.Equal(UnitStatus.Moving, tank.Status);
    }

    [Fact]
    public void DispatchEventsAreSubjectToTheSameRules()
    {
        var world = new WorldState(1, 20, 10) { Territory = Map() };
        world.Countries.Add(new Country(A, "A", new Vector2(4, 5), 100, 100));
        var ship = world.AddUnit(A, UnitKind.Naval, new Vector2(10, 7), 1, 100);
        new UnitDispatchEvent(ship.Id, new Vector2(4, 5)).Apply(world);
        Assert.Equal(UnitStatus.Idle, ship.Status);
        new UnitDispatchEvent(ship.Id, new Vector2(11, 9)).Apply(world);
        Assert.Equal(UnitStatus.Moving, ship.Status);
    }

    [Fact]
    public void WorldsWithoutATerritoryKeepUnrestrictedMovement()
    {
        var world = new WorldState(1, 20, 10);
        world.Countries.Add(new Country(A, "A", new Vector2(4, 5), 100, 100));
        var ship = world.AddUnit(A, UnitKind.Naval, new Vector2(4, 5), 1, 100);
        Assert.True(world.TryOrderMove(ship, new Vector2(19, 9)));
    }

    [Theory]
    [InlineData(UnitKind.Infantry, Domain.Land)]
    [InlineData(UnitKind.Artillery, Domain.Land)]
    [InlineData(UnitKind.Armor, Domain.Land)]
    [InlineData(UnitKind.SupplyConvoy, Domain.Land)]
    [InlineData(UnitKind.Naval, Domain.Sea)]
    [InlineData(UnitKind.Air, Domain.Air)]
    [InlineData(UnitKind.Tank, Domain.Land)]
    [InlineData(UnitKind.ArmoredCar, Domain.Land)]
    [InlineData(UnitKind.Truck, Domain.Land)]
    [InlineData(UnitKind.Warship, Domain.Sea)]
    [InlineData(UnitKind.CargoShip, Domain.Sea)]
    [InlineData(UnitKind.Warplane, Domain.Air)]
    [InlineData(UnitKind.Helicopter, Domain.Air)]
    [InlineData(UnitKind.CargoPlane, Domain.Air)]
    public void EveryUnitKindBelongsToOneDomain(UnitKind kind, Domain expected)
    {
        Assert.Equal(expected, kind.DomainOf());
    }

    [Fact]
    public void ScenarioLoadsTheColdWarTerritory()
    {
        Assert.NotNull(DefaultScenario.Build(42).State.Territory);
    }

    [Theory]
    [InlineData(42)]
    [InlineData(7)]
    public void EveryUnitStartsOnTerrainItsKindCanHold(int seed)
    {
        var state = DefaultScenario.Build(seed).State;
        foreach (var unit in state.Units)
        {
            Assert.True(
                state.Territory!.Permits(unit.Kind.DomainOf(), unit.Owner, unit.Position),
                $"{unit.Owner.Value} {unit.Kind} starts at {unit.Position}");
        }
    }

    [Theory]
    [InlineData(42)]
    [InlineData(7)]
    public void NoUnitEverLeavesLegalTerrainDuringALongRun(int seed)
    {
        var simulation = DefaultScenario.Build(seed);
        var start = simulation.State.Units.ToDictionary(u => u.Id, u => u.Position);
        for (var block = 0; block < 60; block++)
        {
            simulation.Run(25);
            foreach (var unit in simulation.State.Units)
            {
                var domain = unit.Kind.DomainOf();
                var legal = simulation.State.Territory!.Permits(domain, unit.Owner, unit.Position)
                    || (domain == Domain.Land
                        && !simulation.State.Territory.IsSea(unit.Position)
                        && simulation.State.Occupations.HolderAt(unit.Position) == unit.Owner);
                Assert.True(
                    legal,
                    $"tick {simulation.State.Clock.Tick}: {unit.Owner.Value} {unit.Kind} at {unit.Position}");
            }
        }
        var moved = simulation.State.Units.Count(u => u.Position != start[u.Id]);
        Assert.True(moved >= 10, $"only {moved} units moved");
    }
}
