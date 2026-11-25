using System.Numerics;
using Simulation.API.Scenarios;
using Simulation.API.Services;
using WorldEngine.Countries;
using WorldEngine.Diplomacy;
using WorldEngine.Interactions;
using WorldEngine.Routing;
using WorldEngine.Sensors;
using WorldEngine.Territory;
using WorldEngine.Units;
using WorldEngine.World;
using Xunit;

namespace Simulation.Tests;

public class FrontlineTests
{
    private static readonly CountryId A = new("AAA");
    private static readonly CountryId B = new("BBB");
    private static readonly CountryId C = new("CCC");

    private const string BorderJson =
        "{\"width\":200,\"height\":100,\"entities\":["
        + "{\"id\":\"AAA\",\"rings\":[[0,0,100,0,100,100,0,100]]},"
        + "{\"id\":\"BBB\",\"rings\":[[100,0,200,0,200,100,100,100]]}"
        + "]}";

    private const string WallJson =
        "{\"width\":200,\"height\":100,\"entities\":["
        + "{\"id\":\"AAA\",\"rings\":[[0,0,60,0,60,100,0,100]]},"
        + "{\"id\":\"CCC\",\"rings\":[[60,0,140,0,140,100,60,100]]},"
        + "{\"id\":\"BBB\",\"rings\":[[140,0,200,0,200,100,140,100]]}"
        + "]}";

    private static ProxyFront Front(Rung start, Rung ceiling) =>
        new("test front", A, B, start, ceiling, "the border is disputed");

    private static WorldSimulation Border(int seed, int attackers, int defenders, Rung start = Rung.Tension, bool doctrine = true)
    {
        var map = TerritoryLoader.FromJson(BorderJson);
        var world = new WorldState(seed, 200, 100) { Territory = map };
        world.Countries.Add(new Country(A, "Alpha", new Vector2(50, 51), 300, 300, Bloc.East, "L1"));
        world.Countries.Add(new Country(B, "Bravo", new Vector2(150, 51), 300, 300, Bloc.West, "L1"));
        world.Relations.Set(A, B, -0.9);
        for (var i = 0; i < attackers; i++)
        {
            world.AddUnit(A, UnitKind.Tank, new Vector2(70 + i, 51));
        }
        for (var i = 0; i < defenders; i++)
        {
            world.AddUnit(B, UnitKind.Infantry, new Vector2(160 + i, 51));
        }
        if (doctrine)
        {
            world.Doctrine = new Doctrine(null, new[] { Front(start, start) });
            world.Escalation = new EscalationLadder(world.Doctrine);
        }
        world.Interactions.Open(
            InteractionKind.Clash, A, B, TransportMode.None, 0, "border engagement", 0, 5, 5, InteractionStatus.Resolved);
        return new WorldSimulation(world, null, null, new InteractionEngine(null, spawnChance: 0.0));
    }

    private static int SeedWithOccupation(int attackers, int defenders)
    {
        for (var seed = 1; seed <= 12; seed++)
        {
            var simulation = Border(seed, attackers, defenders);
            simulation.Run(10);
            if (simulation.State.Occupations.All.Count > 0)
            {
                return seed;
            }
        }
        return -1;
    }

    [Fact]
    public void AZoneFadesLinearlyAndEndsAtItsRepairTick()
    {
        var zone = new ParalyzedZone(1, A, new Vector2(10, 10), 5, 0.8, 10, 110, "test");
        Assert.Equal(0.8, zone.SeverityAt(10), 6);
        Assert.Equal(0.4, zone.SeverityAt(60), 6);
        Assert.Equal(0.0, zone.SeverityAt(110), 6);
        Assert.Equal(0.0, zone.SeverityAt(500), 6);
        Assert.True(zone.Contains(new Vector2(12, 10)));
        Assert.False(zone.Contains(new Vector2(20, 10)));
    }

    [Fact]
    public void AZoneOnlyBlocksWhileItIsBadEnough()
    {
        var zones = new ZoneBook();
        zones.Add(A, new Vector2(10, 10), 5, 0.8, 10, 110, "test");
        Assert.True(zones.Blocks(A, new Vector2(10, 10), 30));
        Assert.False(zones.Blocks(A, new Vector2(10, 10), 60));
        Assert.False(zones.Blocks(B, new Vector2(10, 10), 30));
        Assert.False(zones.Blocks(A, new Vector2(100, 100), 30));
        Assert.Equal(0.64, zones.Severity(A, 30), 6);
        Assert.Equal(0.0, zones.Severity(B, 30), 6);
        Assert.True(zones.Covers(new Vector2(10, 10), 60));
        Assert.Single(zones.Active(60));
        Assert.Empty(zones.Active(110));
    }

    [Fact]
    public void RepairedZonesAreTakenOutOnce()
    {
        var zones = new ZoneBook();
        zones.Add(A, new Vector2(10, 10), 5, 0.8, 0, 50, "early");
        zones.Add(A, new Vector2(10, 10), 5, 0.8, 0, 90, "late");
        Assert.Empty(zones.TakeRepaired(49));
        Assert.Single(zones.TakeRepaired(50));
        Assert.Empty(zones.TakeRepaired(50));
        Assert.Single(zones.TakeRepaired(90));
    }

    [Fact]
    public void AUnitInAParalyzedPatchCannotMoveUntilItIsRepaired()
    {
        var world = new WorldState(1, 200, 100);
        world.Countries.Add(new Country(A, "Alpha", new Vector2(10, 10), 100, 100));
        var unit = world.AddUnit(A, UnitKind.Infantry, new Vector2(10, 10));
        world.Zones.Add(A, new Vector2(10, 10), 5, 1.0, 0, 100, "test");
        Assert.False(world.TryOrderMove(unit, new Vector2(60, 10)));
        Assert.Equal(UnitStatus.Idle, unit.Status);
        for (var i = 0; i < 100; i++)
        {
            world.Clock.Advance();
        }
        Assert.True(world.TryOrderMove(unit, new Vector2(60, 10)));
        Assert.Equal(UnitStatus.Moving, unit.Status);
    }

    [Fact]
    public void AnOrderIntoAParalyzedPatchIsRefusedToo()
    {
        var world = new WorldState(1, 200, 100);
        world.Countries.Add(new Country(A, "Alpha", new Vector2(10, 10), 100, 100));
        var unit = world.AddUnit(A, UnitKind.Infantry, new Vector2(60, 10));
        world.Zones.Add(A, new Vector2(10, 10), 5, 1.0, 0, 100, "test");
        Assert.False(world.TryOrderMove(unit, new Vector2(10, 10)));
        Assert.True(world.TryOrderMove(unit, new Vector2(70, 10)));
    }

    [Fact]
    public void OccupationsAreFoundByPointAndHandedBackOldestFirst()
    {
        var book = new OccupationBook();
        book.Add(A, B, new Vector2(100, 50), 10, 5, "first");
        book.Add(A, B, new Vector2(100, 20), 10, 40, "second");
        Assert.Equal(A, book.HolderAt(new Vector2(104, 50)));
        Assert.Null(book.HolderAt(new Vector2(150, 50)));
        Assert.Equal(2, book.Count(A, B));
        Assert.Equal(0, book.Count(B, A));
        Assert.Null(book.Release(A, B, 20, 30));
        var first = book.Release(B, A, 40, 30);
        Assert.NotNull(first);
        Assert.Equal("first", first!.Cause);
        Assert.Null(book.Release(A, B, 60, 30));
        Assert.Equal("second", book.Release(A, B, 80, 30)!.Cause);
        Assert.Empty(book.All);
    }

    [Fact]
    public void LandUnitsRespectHeldGround()
    {
        var map = TerritoryLoader.FromJson(BorderJson);
        var world = new WorldState(1, 200, 100) { Territory = map };
        world.Countries.Add(new Country(A, "Alpha", new Vector2(50, 51), 100, 100));
        world.Countries.Add(new Country(B, "Bravo", new Vector2(150, 51), 100, 100));
        world.Occupations.Add(A, B, new Vector2(110, 51), 12, 0, "test");
        var outsider = world.AddUnit(B, UnitKind.Infantry, new Vector2(150, 51));
        Assert.False(world.TryOrderMove(outsider, new Vector2(110, 51)));
        Assert.True(world.TryOrderMove(outsider, new Vector2(140, 51)));
        var home = world.AddUnit(A, UnitKind.Infantry, new Vector2(70, 51));
        Assert.False(world.TryOrderMove(home, new Vector2(110, 51)));
        var garrison = world.AddUnit(A, UnitKind.Infantry, new Vector2(108, 51));
        Assert.True(world.TryOrderMove(garrison, new Vector2(112, 51)));
        Assert.False(world.TryOrderMove(garrison, new Vector2(70, 51)));
    }

    [Fact]
    public void ADecisiveClashSeizesGroundFromTheWeakerSide()
    {
        var seized = 0;
        for (var seed = 1; seed <= 12; seed++)
        {
            var simulation = Border(seed, 12, 1);
            simulation.Run(10);
            var state = simulation.State;
            foreach (var occupation in state.Occupations.All)
            {
                seized++;
                Assert.Equal(A, occupation.Holder);
                Assert.Equal(B, occupation.Loser);
                Assert.Equal(B, state.Territory!.OwnerAt(occupation.Center));
                Assert.InRange(occupation.Radius, 10f, 16f);
                Assert.Contains("outmatched", occupation.Cause);
            }
            if (state.Occupations.All.Count > 0)
            {
                Assert.Contains(
                    state.Log.Records,
                    r => r.Kind == "territory_seized" && r.Description.Contains("because"));
            }
            Assert.True(state.Occupations.Count(A, B) <= InteractionEngine.MaxOccupationsPerPair);
        }
        Assert.True(seized > 0, "no seed produced an occupation");
    }

    [Fact]
    public void AnEvenClashSeizesNothing()
    {
        for (var seed = 1; seed <= 10; seed++)
        {
            var simulation = Border(seed, 0, 0);
            var state = simulation.State;
            state.AddUnit(A, UnitKind.Infantry, new Vector2(60, 51));
            state.AddUnit(B, UnitKind.Infantry, new Vector2(160, 51));
            simulation.Run(10);
            Assert.Empty(state.Occupations.All);
        }
    }

    [Fact]
    public void AWorldWithoutADoctrineNeverChangesHands()
    {
        for (var seed = 1; seed <= 6; seed++)
        {
            var simulation = Border(seed, 12, 1, doctrine: false);
            simulation.Run(40);
            Assert.Empty(simulation.State.Occupations.All);
            Assert.DoesNotContain(simulation.State.Log.Records, r => r.Kind == "territory_seized");
        }
    }

    [Fact]
    public void TheWinnerPutsAGarrisonOnTheGroundItSeized()
    {
        var seed = SeedWithOccupation(12, 1);
        Assert.True(seed > 0);
        var simulation = Border(seed, 12, 1);
        simulation.Run(50);
        var state = simulation.State;
        var occupation = state.Occupations.All[0];
        var inside = state.Units
            .Where(u => u.Owner == A && occupation.Contains(u.Position))
            .ToList();
        Assert.NotEmpty(inside);
        Assert.Contains(inside, u => state.Territory!.OwnerAt(u.Position) == B);
        Assert.All(
            state.Units.Where(u => u.Owner == A),
            u => Assert.True(
                state.Territory!.Permits(Domain.Land, A, u.Position) || state.Occupations.HolderAt(u.Position) == A));
    }

    [Fact]
    public void OccupationCutsTheLosersIncome()
    {
        var seed = SeedWithOccupation(12, 1);
        Assert.True(seed > 0);
        var simulation = Border(seed, 12, 1);
        simulation.Run(10);
        var state = simulation.State;
        Assert.Equal(308.0, state.Countries.Get(A).Supplies, 6);
        Assert.Equal(300 + 8 * (1 - InteractionEngine.OccupationIncomeLoss * state.Occupations.Count(A, B)), state.Countries.Get(B).Supplies, 6);
    }

    [Fact]
    public void GroundIsHandedBackWhenTheFrontCoolsAndTheGarrisonPullsOut()
    {
        var seed = SeedWithOccupation(12, 1);
        Assert.True(seed > 0);
        var simulation = Border(seed, 12, 1);
        simulation.Run(90);
        var state = simulation.State;
        Assert.Empty(state.Occupations.All);
        Assert.Contains(state.Log.Records, r => r.Kind == "ground_returned" && r.Description.Contains("because"));
        Assert.All(
            state.Units.Where(u => u.Owner == A),
            u => Assert.True(state.Territory!.Permits(Domain.Land, A, u.Position), $"unit {u.Id} at {u.Position}"));
    }

    [Fact]
    public void ConquestIsDeterministicForASeed()
    {
        var first = Border(5, 12, 1);
        var second = Border(5, 12, 1);
        first.Run(80);
        second.Run(80);
        Assert.Equal(
            first.State.Log.Records.Select(r => (r.Tick, r.Kind, r.Description)),
            second.State.Log.Records.Select(r => (r.Tick, r.Kind, r.Description)));
        Assert.Equal(
            first.State.Units.Select(u => (u.Id, u.Position, u.Status)),
            second.State.Units.Select(u => (u.Id, u.Position, u.Status)));
    }

    [Fact]
    public void OnlyLandCombatUnitsCanLieInAmbush()
    {
        foreach (var kind in new[] { UnitKind.Infantry, UnitKind.Tank, UnitKind.ArmoredCar })
        {
            Assert.True(AmbushRules.CanAmbush(kind), kind.ToString());
        }
        foreach (var kind in new[]
                 {
                     UnitKind.Artillery, UnitKind.Truck, UnitKind.Warship, UnitKind.Warplane,
                     UnitKind.Helicopter, UnitKind.CargoShip, UnitKind.CargoPlane
                 })
        {
            Assert.False(AmbushRules.CanAmbush(kind), kind.ToString());
        }
    }

    [Fact]
    public void AnAmbushingUnitHoldsItsGroundAndOnlyIdleUnitsTakeCover()
    {
        var world = new WorldState(1, 200, 100);
        world.Countries.Add(new Country(A, "Alpha", new Vector2(50, 51), 100, 100));
        var unit = world.AddUnit(A, UnitKind.Infantry, new Vector2(50, 51));
        unit.Conceal();
        Assert.Equal(UnitStatus.Ambushing, unit.Status);
        Assert.False(world.TryOrderMove(unit, new Vector2(60, 51)));
        Assert.Equal(UnitStatus.Ambushing, unit.Status);
        unit.Reveal();
        Assert.Equal(UnitStatus.Idle, unit.Status);
        Assert.True(world.TryOrderMove(unit, new Vector2(60, 51)));
        unit.Conceal();
        Assert.Equal(UnitStatus.Moving, unit.Status);
    }

    [Fact]
    public void CoverMakesAUnitMuchHarderForSensorsToSee()
    {
        var world = new WorldState(1, 200, 100);
        world.Countries.Add(new Country(A, "Alpha", new Vector2(50, 51), 100, 100));
        world.Countries.Add(new Country(B, "Bravo", new Vector2(150, 51), 100, 100));
        var tank = world.AddUnit(A, UnitKind.Tank, new Vector2(100, 51));
        var radar = SensorProfiles.Radar(1, B, new Vector2(110, 51));
        var open = 0;
        for (var i = 0; i < 400; i++)
        {
            open += radar.Sense(world, new Random(i)).Count;
        }
        tank.Conceal();
        var hidden = 0;
        for (var i = 0; i < 400; i++)
        {
            hidden += radar.Sense(world, new Random(i)).Count;
        }
        Assert.True(open > 100, $"only {open} sightings in the open");
        Assert.True(hidden < open * 0.4, $"{hidden} sightings under cover against {open} in the open");
    }

    [Fact]
    public void TroopsTakeCoverInsideTheirBorderOnAHotFront()
    {
        var simulation = Border(3, 6, 6, start: Rung.Incident);
        simulation.Run(58);
        var state = simulation.State;
        Assert.Contains(state.Log.Records, r => r.Kind == "ambush_set");
        var hiddenA = state.Units.Count(u => u.Owner == A && u.Status == UnitStatus.Ambushing);
        var hiddenB = state.Units.Count(u => u.Owner == B && u.Status == UnitStatus.Ambushing);
        Assert.InRange(hiddenA, 0, InteractionEngine.AmbushesPerCountry);
        Assert.InRange(hiddenB, 0, InteractionEngine.AmbushesPerCountry);
        Assert.True(hiddenA + hiddenB > 0);
        foreach (var unit in state.Units.Where(u => u.Status == UnitStatus.Ambushing))
        {
            Assert.True(state.Territory!.Permits(Domain.Land, unit.Owner, unit.Position));
            Assert.True(AmbushRules.CanAmbush(unit.Kind));
        }
    }

    [Fact]
    public void AHiddenUnitSpringsOnAHostileUnitThatComesClose()
    {
        for (var seed = 1; seed <= 6; seed++)
        {
            var map = TerritoryLoader.FromJson(BorderJson);
            var world = new WorldState(seed, 200, 100) { Territory = map };
            world.Countries.Add(new Country(A, "Alpha", new Vector2(50, 51), 300, 300, Bloc.East, "L1"));
            world.Countries.Add(new Country(B, "Bravo", new Vector2(150, 51), 300, 300, Bloc.West, "L1"));
            world.Relations.Set(A, B, -0.9);
            var doctrine = new Doctrine(null, new[] { Front(Rung.Calm, Rung.Calm) });
            world.Doctrine = doctrine;
            world.Escalation = new EscalationLadder(doctrine);
            var ambusher = world.AddUnit(A, UnitKind.Tank, new Vector2(98, 51));
            var victim = world.AddUnit(B, UnitKind.Tank, new Vector2(96, 51));
            ambusher.Conceal();
            var simulation = new WorldSimulation(world, null, null, new InteractionEngine(null, spawnChance: 0.0));
            simulation.Run(30);
            Assert.Contains(world.Log.Records, r => r.Kind == "ambush_sprung" && r.Description.Contains("because"));
            Assert.Equal(UnitStatus.Idle, ambusher.Status);
            Assert.True(victim.Strength < 100, $"seed {seed}: the victim was not hurt");
        }
    }

    [Fact]
    public void PeacefulNeighboursAreLeftAlone()
    {
        var map = TerritoryLoader.FromJson(BorderJson);
        var world = new WorldState(1, 200, 100) { Territory = map };
        world.Countries.Add(new Country(A, "Alpha", new Vector2(50, 51), 300, 300, Bloc.East, "L1"));
        world.Countries.Add(new Country(B, "Bravo", new Vector2(150, 51), 300, 300, Bloc.West, "L1"));
        world.Relations.Set(A, B, 0.6);
        var doctrine = new Doctrine(null, new[] { Front(Rung.Calm, Rung.Calm) });
        world.Doctrine = doctrine;
        world.Escalation = new EscalationLadder(doctrine);
        var ambusher = world.AddUnit(A, UnitKind.Tank, new Vector2(98, 51));
        world.AddUnit(B, UnitKind.Tank, new Vector2(96, 51));
        ambusher.Conceal();
        var simulation = new WorldSimulation(world, null, null, new InteractionEngine(null, spawnChance: 0.0));
        simulation.Run(30);
        Assert.DoesNotContain(world.Log.Records, r => r.Kind == "ambush_sprung");
        Assert.Equal(UnitStatus.Ambushing, ambusher.Status);
    }

    [Fact]
    public void AHiddenUnitSpringsOnAHostileConvoyThatDrivesPast()
    {
        var map = TerritoryLoader.FromJson(WallJson);
        var world = new WorldState(11, 200, 100) { Territory = map };
        world.Routes = new RouteNetwork(map, Array.Empty<Port>());
        world.Countries.Add(new Country(A, "Alpha", new Vector2(10, 51), 5000, 500, Bloc.East, "L1"));
        world.Countries.Add(new Country(C, "Charlie", new Vector2(100, 51), 300, 500, Bloc.NonAligned, "L1"));
        world.Countries.Add(new Country(B, "Bravo", new Vector2(190, 51), 200, 500, Bloc.East, "L1"));
        world.Relations.Set(A, B, 0.8);
        world.Relations.Set(A, C, -0.9);
        world.Relations.Set(B, C, -0.9);
        var ambusher = world.AddUnit(C, UnitKind.Tank, new Vector2(100, 51));
        ambusher.Conceal();
        var doctrine = new Doctrine(new[] { new AidTie(A, B, "Alpha keeps Bravo supplied") });
        world.Doctrine = doctrine;
        world.Escalation = new EscalationLadder(doctrine);
        var simulation = new WorldSimulation(world, null, null, new InteractionEngine(null, spawnChance: 1.0));
        var caught = new Dictionary<int, Interaction>();
        for (var step = 0; step < 750; step++)
        {
            simulation.Run(2);
            foreach (var item in world.Interactions.All.Where(i => i.Kind == InteractionKind.SupplyShipment))
            {
                caught[item.Id] = item;
            }
        }
        Assert.Contains(world.Log.Records, r => r.Kind == "ambush_sprung" && r.Description.Contains("convoy"));
        Assert.Contains(caught.Values, i => i.Status == InteractionStatus.Intercepted && i.InterceptedBy == C);
    }

    [Fact]
    public void AMissileThatLandsParalyzesGroundAndASecondaryBlastCanFollow()
    {
        var doctrine = new Doctrine(null, new[] { Front(Rung.Missile, Rung.Missile) });
        var world = new WorldState(5, 2000, 1000);
        world.Countries.Add(new Country(A, "Alpha", new Vector2(200, 300), 600, 900, Bloc.East, "L1"));
        world.Countries.Add(new Country(B, "Bravo", new Vector2(900, 400), 600, 900, Bloc.West, "L2"));
        world.Relations.Set(A, B, -0.95);
        world.Doctrine = doctrine;
        world.Escalation = new EscalationLadder(doctrine);
        var simulation = new WorldSimulation(world, null, null, new InteractionEngine(null, spawnChance: 1.0));
        var zonesSeen = new Dictionary<int, ParalyzedZone>();
        for (var step = 0; step < 300; step++)
        {
            simulation.Run(2);
            foreach (var zone in world.Zones.Active(world.Clock.Tick))
            {
                zonesSeen[zone.Id] = zone;
            }
        }
        Assert.NotEmpty(zonesSeen);
        Assert.All(zonesSeen.Values, z => Assert.InRange(z.Severity, 0.6, 1.0));
        Assert.Contains(world.Log.Records, r => r.Kind == "zone_paralyzed");
        Assert.Contains(world.Log.Records, r => r.Kind == "zone_repaired");
        Assert.Contains(world.Log.Records, r => r.Kind == "secondary_explosion");
    }

    [Fact]
    public void ACountryWithParalyzedGroundStopsShipping()
    {
        var doctrine = new Doctrine(new[] { new AidTie(A, C, "Alpha backs Charlie") });
        var world = new WorldState(5, 2000, 1000);
        world.Countries.Add(new Country(A, "Alpha", new Vector2(200, 300), 600, 500, Bloc.East, "L1"));
        world.Countries.Add(new Country(B, "Bravo", new Vector2(900, 400), 600, 500, Bloc.West, "L2"));
        world.Countries.Add(new Country(C, "Charlie", new Vector2(500, 700), 200, 500, Bloc.East, "L1"));
        world.Relations.Set(A, B, -0.5);
        world.Relations.Set(A, C, 0.6);
        world.Relations.Set(B, C, 0.1);
        world.Doctrine = doctrine;
        world.Escalation = new EscalationLadder(doctrine);
        world.Zones.Add(A, new Vector2(200, 300), 10, 1.0, 0, 1_000_000, "test");
        var simulation = new WorldSimulation(world, null, null, new InteractionEngine(null, spawnChance: 1.0));
        simulation.Run(200);
        Assert.DoesNotContain(world.Log.Records, r => r.Kind == "supply_dispatched" && r.Description.StartsWith("Alpha"));
    }

    [Fact]
    public void TheSnapshotCarriesLiveZonesAndOccupations()
    {
        var map = TerritoryLoader.FromJson(BorderJson);
        var world = new WorldState(1, 200, 100) { Territory = map };
        world.Countries.Add(new Country(A, "Alpha", new Vector2(50, 51), 300, 300, Bloc.East, "L1"));
        world.Countries.Add(new Country(B, "Bravo", new Vector2(150, 51), 300, 300, Bloc.West, "L1"));
        world.Zones.Add(A, new Vector2(40, 50), 8, 0.8, 0, 100, "missile strike");
        world.Zones.Add(A, new Vector2(20, 50), 5, 0.5, 0, 10, "old strike");
        world.Occupations.Add(B, A, new Vector2(90, 50), 12, 3, "Bravo outmatched Alpha in the fighting");
        var simulation = new WorldSimulation(world, null, null, new InteractionEngine(null, spawnChance: 0.0));
        simulation.Run(20);
        var tick = world.Clock.Tick;
        var snapshot = GroundTruthMapper.Map(simulation);
        Assert.NotNull(snapshot.Zones);
        var zone = Assert.Single(snapshot.Zones!);
        Assert.Equal(A.Value, zone.Owner);
        Assert.Equal(40f, zone.X);
        Assert.Equal(50f, zone.Y);
        Assert.Equal(8f, zone.Radius);
        Assert.Equal(Math.Round(world.Zones.Active(tick)[0].SeverityAt(tick), 3), zone.Severity, 6);
        Assert.InRange(zone.Severity, 0.0, 0.8);
        Assert.Equal(100L, zone.RepairTick);
        Assert.Equal("missile strike", zone.Cause);
        Assert.Contains(world.Log.Records, r => r.Kind == "zone_repaired");
        Assert.NotNull(snapshot.Occupations);
        var held = Assert.Single(snapshot.Occupations!);
        Assert.Equal(B.Value, held.Holder);
        Assert.Equal(A.Value, held.Loser);
        Assert.Equal(90f, held.X);
        Assert.Equal(12f, held.Radius);
        Assert.Equal(3L, held.StartTick);
        Assert.Contains("outmatched", held.Cause);
    }

    [Fact]
    public void ASnapshotWithNoFrontlineStateHasEmptyListsNotNulls()
    {
        var world = new WorldState(1, 200, 100);
        world.Countries.Add(new Country(A, "Alpha", new Vector2(50, 51), 300, 300));
        var simulation = new WorldSimulation(world, null, null, new InteractionEngine(null, spawnChance: 0.0));
        var snapshot = GroundTruthMapper.Map(simulation);
        Assert.NotNull(snapshot.Zones);
        Assert.Empty(snapshot.Zones!);
        Assert.NotNull(snapshot.Occupations);
        Assert.Empty(snapshot.Occupations!);
    }

    [Fact]
    public void AnAmbushingUnitReachesTheSnapshotWithItsStatus()
    {
        var world = new WorldState(1, 200, 100);
        world.Countries.Add(new Country(A, "Alpha", new Vector2(50, 51), 300, 300));
        var unit = world.AddUnit(A, UnitKind.Tank, new Vector2(50, 51));
        unit.Conceal();
        var simulation = new WorldSimulation(world, null, null, new InteractionEngine(null, spawnChance: 0.0));
        var snapshot = GroundTruthMapper.Map(simulation);
        Assert.Equal("Ambushing", Assert.Single(snapshot.Units).Status);
    }

    [Theory]
    [InlineData(42)]
    [InlineData(7)]
    public void ADefaultWorldKeepsItsFrontlineStateConsistent(int seed)
    {
        var simulation = DefaultScenario.Build(seed);
        var state = simulation.State;
        var doctrine = state.Doctrine!;
        var map = state.Territory!;
        for (var block = 0; block < 120; block++)
        {
            simulation.Run(25);
            var tick = state.Clock.Tick;
            foreach (var occupation in state.Occupations.All)
            {
                Assert.NotEqual(occupation.Holder, occupation.Loser);
                Assert.True(doctrine.MayFight(occupation.Holder, occupation.Loser), $"{occupation.Holder.Value} holds ground of {occupation.Loser.Value}");
                Assert.Equal(occupation.Loser, map.OwnerAt(occupation.Center));
                Assert.True(state.Occupations.Count(occupation.Holder, occupation.Loser) <= InteractionEngine.MaxOccupationsPerPair);
            }
            foreach (var zone in state.Zones.Active(tick))
            {
                Assert.True(state.Countries.Contains(zone.Owner));
                Assert.InRange(zone.SeverityAt(tick), 0.0, 1.0);
                Assert.True(zone.RepairTick > zone.StartTick);
            }
            var hidden = state.Units.Where(u => u.Status == UnitStatus.Ambushing).ToList();
            Assert.All(hidden, u => Assert.True(AmbushRules.CanAmbush(u.Kind)));
            Assert.True(hidden.Count <= InteractionEngine.MaxAmbushes + 2, $"{hidden.Count} units in ambush");
        }
    }
}
