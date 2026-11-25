using System.Numerics;
using Simulation.API.Scenarios;
using WorldEngine.Countries;
using WorldEngine.Diplomacy;
using WorldEngine.Interactions;
using WorldEngine.Routing;
using WorldEngine.Territory;
using WorldEngine.Units;
using WorldEngine.World;
using Xunit;

namespace Simulation.Tests;

public class DoctrineTests
{
    private static readonly CountryId A = new("AAA");
    private static readonly CountryId B = new("BBB");
    private static readonly CountryId C = new("CCC");

    private const string WallJson =
        "{\"width\":200,\"height\":100,\"entities\":["
        + "{\"id\":\"AAA\",\"rings\":[[0,0,60,0,60,100,0,100]]},"
        + "{\"id\":\"CCC\",\"rings\":[[60,0,140,0,140,100,60,100]]},"
        + "{\"id\":\"BBB\",\"rings\":[[140,0,200,0,200,100,140,100]]}"
        + "]}";

    private const string IslandJson =
        "{\"width\":200,\"height\":100,\"entities\":["
        + "{\"id\":\"AAA\",\"rings\":[[0,0,60,0,60,100,0,100]]},"
        + "{\"id\":\"BBB\",\"rings\":[[140,0,200,0,200,100,140,100]]},"
        + "{\"id\":\"CCC\",\"rings\":[[90,40,110,40,110,60,90,60]]}"
        + "]}";

    private static ProxyFront Front(Rung start, Rung ceiling) =>
        new("test front", A, B, start, ceiling, "the border is disputed");

    private static WorldSimulation Bare(int seed, Doctrine doctrine, double relationAB = -0.95, double supplyC = 600)
    {
        var world = new WorldState(seed, 2000, 1000);
        world.Countries.Add(new Country(A, "Alpha", new Vector2(200, 300), 600, 500, Bloc.East, "L1"));
        world.Countries.Add(new Country(B, "Bravo", new Vector2(900, 400), 600, 500, Bloc.West, "L2"));
        world.Countries.Add(new Country(C, "Charlie", new Vector2(500, 700), supplyC, 500, Bloc.NonAligned, "L1"));
        world.Relations.Set(A, B, relationAB);
        world.Relations.Set(A, C, 0.6);
        world.Relations.Set(B, C, 0.4);
        world.Doctrine = doctrine;
        world.Escalation = new EscalationLadder(doctrine);
        return new WorldSimulation(world, null, null, new InteractionEngine(null, spawnChance: 1.0));
    }

    private static Dictionary<int, Interaction> Collect(WorldSimulation simulation, int steps, int stride)
    {
        var seen = new Dictionary<int, Interaction>();
        for (var step = 0; step < steps; step++)
        {
            simulation.Run(stride);
            foreach (var item in simulation.State.Interactions.All)
            {
                seen[item.Id] = item;
            }
        }
        return seen;
    }

    [Fact]
    public void PairsAreFoundInEitherOrder()
    {
        var doctrine = new Doctrine(
            new[] { new AidTie(A, C, "aid") },
            new[] { Front(Rung.Calm, Rung.Clash) },
            new[] { new SummitTie(A, B, "arms limitation", "standoff") },
            new[] { (B, C) });
        Assert.NotNull(doctrine.AidFor(A, C));
        Assert.Null(doctrine.AidFor(C, A));
        Assert.NotNull(doctrine.FrontOf(B, A));
        Assert.NotNull(doctrine.SummitOf(B, A));
        Assert.True(doctrine.Forbids(C, B));
        Assert.True(doctrine.MayFight(B, A));
        Assert.False(doctrine.MayFight(A, C));
    }

    [Fact]
    public void AForbiddenPairCannotFightEvenOnAFront()
    {
        var doctrine = new Doctrine(null, new[] { Front(Rung.Clash, Rung.Clash) }, null, new[] { (A, B) });
        Assert.False(doctrine.MayFight(A, B));
    }

    [Fact]
    public void AFrontCannotStartAboveItsCeiling()
    {
        Assert.Throws<ArgumentException>(() => new Doctrine(null, new[] { Front(Rung.Missile, Rung.Incident) }));
    }

    [Fact]
    public void TheLadderClimbsWhileHostileAndNeverPastTheCeiling()
    {
        var doctrine = new Doctrine(null, new[] { Front(Rung.Calm, Rung.Incident) });
        var state = Bare(3, doctrine).State;
        var ladder = state.Escalation!;
        var highest = Rung.Calm;
        for (var tick = 0; tick < 2000; tick++)
        {
            state.Clock.Advance();
            state.Relations.Set(A, B, -0.95);
            if (state.Clock.Tick % EscalationLadder.CheckInterval == 0)
            {
                ladder.Step(state);
            }
            highest = (Rung)Math.Max((int)highest, (int)ladder.RungOf(A, B));
        }
        Assert.Equal(Rung.Incident, highest);
    }

    [Fact]
    public void TheLadderReachesTheTopOnAHostileFront()
    {
        var doctrine = new Doctrine(null, new[] { Front(Rung.Calm, Rung.Missile) });
        var state = Bare(3, doctrine).State;
        var ladder = state.Escalation!;
        var highest = Rung.Calm;
        for (var tick = 0; tick < 3000; tick++)
        {
            state.Clock.Advance();
            state.Relations.Set(A, B, -0.95);
            if (state.Clock.Tick % EscalationLadder.CheckInterval == 0)
            {
                ladder.Step(state);
            }
            highest = (Rung)Math.Max((int)highest, (int)ladder.RungOf(A, B));
        }
        Assert.Equal(Rung.Missile, highest);
    }

    [Fact]
    public void TheLadderCoolsDownWhenRelationsAreGood()
    {
        var doctrine = new Doctrine(null, new[] { Front(Rung.Clash, Rung.Missile) });
        var state = Bare(3, doctrine, relationAB: 0.5).State;
        var ladder = state.Escalation!;
        for (var tick = 0; tick < 70; tick++)
        {
            state.Clock.Advance();
            if (state.Clock.Tick % EscalationLadder.CheckInterval == 0)
            {
                ladder.Step(state);
            }
        }
        Assert.Equal(Rung.Incident, ladder.RungOf(A, B));
        for (var tick = 0; tick < 200; tick++)
        {
            state.Clock.Advance();
            if (state.Clock.Tick % EscalationLadder.CheckInterval == 0)
            {
                ladder.Step(state);
            }
        }
        Assert.Equal(Rung.Calm, ladder.RungOf(A, B));
    }

    [Fact]
    public void CeasefireTalksLowerOneRung()
    {
        var doctrine = new Doctrine(null, new[] { Front(Rung.Clash, Rung.Missile) });
        var ladder = Bare(3, doctrine).State.Escalation!;
        var move = ladder.Cool(B, A, 10);
        Assert.NotNull(move);
        Assert.Equal(Rung.Clash, move!.Value.From);
        Assert.Equal(Rung.Incident, move.Value.To);
        Assert.Null(new EscalationLadder(new Doctrine()).Cool(A, B, 10));
    }

    [Fact]
    public void HostilePairsWithoutAFrontNeverFight()
    {
        var simulation = Bare(5, new Doctrine());
        simulation.Run(400);
        Assert.DoesNotContain(
            simulation.State.Log.Records,
            r => r.Kind.StartsWith("clash") || r.Kind.StartsWith("missile"));
    }

    [Fact]
    public void ACalmFrontStaysQuietEvenWhenRelationsAreHostile()
    {
        var simulation = Bare(5, new Doctrine(null, new[] { Front(Rung.Calm, Rung.Calm) }));
        simulation.Run(400);
        Assert.DoesNotContain(
            simulation.State.Log.Records,
            r => r.Kind.StartsWith("clash") || r.Kind.StartsWith("missile"));
    }

    [Fact]
    public void AFrontAtClashLevelProducesClashesWithACause()
    {
        var simulation = Bare(5, new Doctrine(null, new[] { Front(Rung.Clash, Rung.Clash) }));
        simulation.Run(300);
        var clashes = simulation.State.Log.Records.Where(r => r.Kind == "clash_started").ToList();
        Assert.NotEmpty(clashes);
        Assert.All(clashes, r => Assert.Contains("because", r.Description));
        Assert.DoesNotContain(simulation.State.Log.Records, r => r.Kind.StartsWith("missile"));
    }

    [Fact]
    public void AFrontAtMissileLevelLaunchesMissiles()
    {
        var simulation = Bare(5, new Doctrine(null, new[] { Front(Rung.Missile, Rung.Missile) }));
        simulation.Run(200);
        Assert.Contains(simulation.State.Log.Records, r => r.Kind == "missile_launched" && r.Description.Contains("because"));
    }

    [Fact]
    public void AForbiddenPairNeverFightsEvenOnAFrontAtTheTop()
    {
        var doctrine = new Doctrine(null, new[] { Front(Rung.Missile, Rung.Missile) }, null, new[] { (A, B) });
        var simulation = Bare(5, doctrine);
        simulation.Run(300);
        Assert.DoesNotContain(
            simulation.State.Log.Records,
            r => r.Kind.StartsWith("clash") || r.Kind.StartsWith("missile"));
    }

    [Fact]
    public void FrontsMoveInTheLogWithTheirReason()
    {
        var simulation = Bare(5, new Doctrine(null, new[] { Front(Rung.Calm, Rung.Missile) }));
        simulation.Run(1500);
        var moves = simulation.State.Log.Records.Where(r => r.Kind == "front_escalated").ToList();
        Assert.NotEmpty(moves);
        Assert.Contains(moves, r => r.Description.Contains("because the border is disputed"));
    }

    [Fact]
    public void ShipmentsFlowOnlyAlongAidTies()
    {
        var doctrine = new Doctrine(new[] { new AidTie(A, C, "Alpha backs Charlie") });
        var simulation = Bare(5, doctrine, supplyC: 200);
        var shipments = Collect(simulation, 150, 3).Values.Where(i => i.Kind == InteractionKind.SupplyShipment).ToList();
        Assert.NotEmpty(shipments);
        Assert.All(shipments, i =>
        {
            Assert.Equal(A, i.Source);
            Assert.Equal(C, i.Target);
            Assert.Contains("Alpha backs Charlie", i.Cause);
        });
        Assert.All(
            simulation.State.Log.Records.Where(r => r.Kind == "supply_dispatched"),
            r => Assert.Contains("because", r.Description));
    }

    [Fact]
    public void SummitTalksHappenAndStateWhy()
    {
        var doctrine = new Doctrine(null, null, new[] { new SummitTie(A, B, "arms limitation", "the standoff demands it") });
        var simulation = Bare(5, doctrine);
        simulation.Run(600);
        Assert.Contains(
            simulation.State.Log.Records,
            r => r.Kind == "negotiation_started" && r.Description.Contains("arms limitation") && r.Description.Contains("the standoff demands it"));
    }

    [Fact]
    public void WithoutADoctrineNothingCarriesACause()
    {
        var world = new WorldState(5, 2000, 1000);
        world.Countries.Add(new Country(A, "Alpha", new Vector2(200, 300), 600, 500, Bloc.East, "L1"));
        world.Countries.Add(new Country(B, "Bravo", new Vector2(900, 400), 600, 500, Bloc.West, "L2"));
        world.Relations.Set(A, B, -0.95);
        var simulation = new WorldSimulation(world, null, null, new InteractionEngine(null, spawnChance: 1.0));
        var seen = Collect(simulation, 100, 3);
        Assert.NotEmpty(seen);
        Assert.All(seen.Values, i => Assert.Equal("", i.Cause));
        Assert.Null(world.Escalation);
        Assert.DoesNotContain(world.Log.Records, r => r.Kind.StartsWith("front_") || r.Description.Contains("because"));
    }

    private static WorldSimulation Corridor(int seed, double relationToMiddle)
    {
        var map = TerritoryLoader.FromJson(WallJson);
        var world = new WorldState(seed, 200, 100);
        world.Territory = map;
        world.Routes = new RouteNetwork(map, Array.Empty<Port>());
        world.Countries.Add(new Country(A, "Alpha", new Vector2(10, 51), 5000, 500, Bloc.East, "L1"));
        world.Countries.Add(new Country(C, "Charlie", new Vector2(100, 51), 300, 500, Bloc.NonAligned, "L1"));
        world.Countries.Add(new Country(B, "Bravo", new Vector2(190, 51), 200, 500, Bloc.East, "L1"));
        world.Relations.Set(A, B, 0.8);
        world.Relations.Set(A, C, relationToMiddle);
        world.Relations.Set(B, C, relationToMiddle);
        world.AddUnit(C, UnitKind.Tank, new Vector2(100, 51));
        world.AddUnit(C, UnitKind.Warplane, new Vector2(100, 40));
        var doctrine = new Doctrine(new[] { new AidTie(A, B, "Alpha keeps Bravo supplied") });
        world.Doctrine = doctrine;
        world.Escalation = new EscalationLadder(doctrine);
        return new WorldSimulation(world, null, null, new InteractionEngine(null, spawnChance: 1.0));
    }

    [Fact]
    public void ShipmentsThroughAHostileCountryAreTrackedAndHuntedByThatCountry()
    {
        var simulation = Corridor(11, -0.9);
        var seen = Collect(simulation, 1500, 2).Values.Where(i => i.Kind == InteractionKind.SupplyShipment).ToList();
        Assert.NotEmpty(seen);
        var caught = seen.Where(i => i.Status == InteractionStatus.Intercepted).ToList();
        Assert.NotEmpty(caught);
        Assert.All(caught, i => Assert.Equal(C, i.InterceptedBy));
        Assert.All(seen.Where(i => i.EndTick is not null && i.Status != InteractionStatus.Intercepted), i => Assert.Equal(InteractionStatus.Succeeded, i.Status));
        Assert.Contains(simulation.State.Log.Records, r => r.Kind == "shipment_tracked" && r.Description.Contains("because relations are hostile"));
        Assert.Contains(simulation.State.Log.Records, r => r.Kind == "supply_intercepted" && r.Description.Contains("seized"));
        Assert.All(seen, i => Assert.Contains("no safe lane exists", i.Cause));
    }

    [Fact]
    public void AnInterceptionEndsTheShipmentWhereItWasCaught()
    {
        var simulation = Corridor(11, -0.9);
        var seen = Collect(simulation, 1500, 2).Values.Where(i => i.Status == InteractionStatus.Intercepted).ToList();
        Assert.NotEmpty(seen);
        Assert.All(seen, i =>
        {
            Assert.NotNull(i.EndTick);
            Assert.True(i.EndTick < i.DueTick);
            Assert.True(i.ProgressAt(i.EndTick!.Value + 50) < 1.0);
        });
    }

    [Fact]
    public void PeacefulTransitIsNeverIntercepted()
    {
        var simulation = Corridor(11, 0.5);
        var seen = Collect(simulation, 800, 2).Values.Where(i => i.Kind == InteractionKind.SupplyShipment).ToList();
        Assert.NotEmpty(seen);
        Assert.All(seen, i => Assert.Null(i.Tracker));
        Assert.All(seen, i => Assert.NotEqual(InteractionStatus.Intercepted, i.Status));
        Assert.DoesNotContain(simulation.State.Log.Records, r => r.Kind == "shipment_tracked" || r.Kind == "supply_intercepted");
    }

    [Fact]
    public void AHostileCountryWithNoInterceptorsCanOnlyWatch()
    {
        var simulation = Corridor(11, -0.9);
        foreach (var unit in simulation.State.Units.Where(u => u.Owner == C))
        {
            unit.ApplyDamage(1e9);
        }
        var seen = Collect(simulation, 800, 2).Values.Where(i => i.Kind == InteractionKind.SupplyShipment).ToList();
        Assert.NotEmpty(seen);
        Assert.All(seen, i => Assert.NotEqual(InteractionStatus.Intercepted, i.Status));
    }

    [Fact]
    public void CoastalWatersBelongToTheirCountryAndOpenSeaToNobody()
    {
        var map = TerritoryLoader.FromJson(IslandJson);
        var open = new RouteNetwork(map, Array.Empty<Port>());
        var waters = new RouteNetwork(map, Array.Empty<Port>(), null, 10f);
        Assert.False(open.HasWaters);
        Assert.True(waters.HasWaters);
        Assert.Equal(C, waters.ZoneAt(new Vector2(100, 50)));
        Assert.Equal(C, waters.ZoneAt(new Vector2(100, 35)));
        Assert.Null(waters.ZoneAt(new Vector2(100, 10)));
        Assert.Null(open.ZoneAt(new Vector2(100, 35)));
        Assert.Equal(A, waters.ZoneAt(new Vector2(30, 50)));
    }

    [Fact]
    public void ShipsPlanAroundHostileWaters()
    {
        var map = TerritoryLoader.FromJson(IslandJson);
        var ports = new[]
        {
            new Port("AAA-1", A, "Alpha Harbour", new Vector2(61, 51)),
            new Port("BBB-1", B, "Bravo Harbour", new Vector2(139, 51))
        };
        var network = new RouteNetwork(map, ports, null, 10f);
        var source = new Country(A, "Alpha", new Vector2(10, 51), 100, 100, Bloc.East, "L1");
        var target = new Country(B, "Bravo", new Vector2(190, 51), 100, 100, Bloc.West, "L2");
        var middle = new Country(C, "Charlie", new Vector2(100, 50), 100, 100, Bloc.NonAligned, "L3");
        var countries = new[] { source, middle, target };
        Func<CountryId, bool> hostile = id => id == C;
        var route = network.Plan(TransportMode.Naval, source, target, countries, hostile);
        Assert.NotNull(route);
        Assert.True(network.IsLegal(route!, source, target, countries, hostile));
        foreach (var leg in route.Legs.Where(l => l.Carrier == UnitKind.CargoShip))
        {
            for (var i = 1; i < leg.Points.Count; i++)
            {
                for (var s = 0; s <= 40; s++)
                {
                    var point = Vector2.Lerp(leg.Points[i - 1], leg.Points[i], s / 40f);
                    Assert.NotEqual(C, network.ZoneAt(point));
                }
            }
        }
    }

    [Fact]
    public void TheDefaultDoctrineNamesOnlyRealCountriesAndFriendlyAid()
    {
        var state = DefaultScenario.Build(42).State;
        var doctrine = state.Doctrine!;
        Assert.NotNull(doctrine);
        Assert.NotEmpty(doctrine.Aid);
        Assert.NotEmpty(doctrine.Fronts);
        Assert.NotEmpty(doctrine.Summits);
        foreach (var tie in doctrine.Aid)
        {
            Assert.True(state.Countries.Contains(tie.Patron) && state.Countries.Contains(tie.Client));
            Assert.True(state.Relations.Get(tie.Patron, tie.Client) > 0, $"{tie.Patron.Value} to {tie.Client.Value}");
        }
        foreach (var front in doctrine.Fronts)
        {
            Assert.True(state.Countries.Contains(front.First) && state.Countries.Contains(front.Second));
            Assert.False(doctrine.Forbids(front.First, front.Second), front.Name);
        }
        Assert.True(doctrine.Forbids(new CountryId("USSR"), new CountryId("USA")));
        Assert.True(doctrine.Forbids(new CountryId("USSR"), new CountryId("FRG")));
        Assert.Equal(doctrine.Fronts.Count, state.Escalation!.Fronts.Count);
    }

    [Theory]
    [InlineData(42)]
    [InlineData(7)]
    public void ADefaultWorldKeepsToItsDoctrineOverALongRun(int seed)
    {
        var simulation = DefaultScenario.Build(seed);
        var state = simulation.State;
        var doctrine = state.Doctrine!;
        var seen = Collect(simulation, 300, 5).Values.ToList();
        Assert.NotEmpty(seen);
        Assert.All(seen, i => Assert.False(string.IsNullOrEmpty(i.Cause), $"{i.Kind} {i.Source.Value}-{i.Target.Value} has no cause"));
        foreach (var item in seen)
        {
            switch (item.Kind)
            {
                case InteractionKind.Clash:
                case InteractionKind.MissileStrike:
                    Assert.True(doctrine.MayFight(item.Source, item.Target), $"{item.Source.Value} vs {item.Target.Value}");
                    break;
                case InteractionKind.SupplyShipment:
                    var sender = state.Countries.Get(item.Source);
                    var receiver = state.Countries.Get(item.Target);
                    var tied = doctrine.AidFor(item.Source, item.Target) is not null
                        || (sender.Bloc == receiver.Bloc && sender.Bloc != Bloc.NonAligned);
                    Assert.True(tied, $"{item.Source.Value} shipped to {item.Target.Value} with no tie");
                    if (item.Status == InteractionStatus.Intercepted)
                    {
                        Assert.NotNull(item.InterceptedBy);
                    }
                    break;
            }
        }
        Assert.Contains(seen, i => i.Kind == InteractionKind.SupplyShipment);
    }
}
