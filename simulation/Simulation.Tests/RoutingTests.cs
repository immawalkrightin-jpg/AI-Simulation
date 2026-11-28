using System.Numerics;
using Simulation.API.Scenarios;
using Simulation.API.Services;
using WorldEngine.Countries;
using WorldEngine.Interactions;
using WorldEngine.Routing;
using WorldEngine.Territory;
using WorldEngine.Units;
using WorldEngine.World;
using Xunit;

namespace Simulation.Tests;

public class RoutingTests
{
    private static readonly CountryId A = new("AAA");
    private static readonly CountryId B = new("BBB");
    private static readonly CountryId C = new("CCC");

    private const string StraitJson =
        "{\"width\":200,\"height\":100,\"entities\":["
        + "{\"id\":\"AAA\",\"rings\":[[0,0,60,0,60,100,0,100]]},"
        + "{\"id\":\"BBB\",\"rings\":[[140,0,200,0,200,100,140,100]]}"
        + "]}";

    private const string IslandJson =
        "{\"width\":200,\"height\":100,\"entities\":["
        + "{\"id\":\"AAA\",\"rings\":[[0,0,60,0,60,100,0,100]]},"
        + "{\"id\":\"BBB\",\"rings\":[[140,0,200,0,200,100,140,100],[90,10,110,10,110,30,90,30]]}"
        + "]}";

    private const string WallJson =
        "{\"width\":200,\"height\":100,\"entities\":["
        + "{\"id\":\"AAA\",\"rings\":[[0,0,60,0,60,100,0,100]]},"
        + "{\"id\":\"CCC\",\"rings\":[[60,0,140,0,140,100,60,100]]},"
        + "{\"id\":\"BBB\",\"rings\":[[140,0,200,0,200,100,140,100]]}"
        + "]}";

    private sealed record Setup(RouteNetwork Network, Country Source, Country Target, IReadOnlyList<Country> Countries);

    private static Setup Strait(string json, Vector2 targetCapital, bool withPorts = true)
    {
        var map = TerritoryLoader.FromJson(json);
        var source = new Country(A, "Alpha", new Vector2(10, 51), 100, 100, Bloc.East, "L1");
        var target = new Country(B, "Bravo", targetCapital, 100, 100, Bloc.West, "L2");
        var ports = withPorts
            ? new[]
            {
                new Port("AAA-1", A, "Alpha Harbour", new Vector2(61, 51)),
                new Port("BBB-1", B, "Bravo Harbour", new Vector2(139, 51))
            }
            : Array.Empty<Port>();
        return new Setup(new RouteNetwork(map, ports), source, target, new[] { source, target });
    }

    private static Setup Wall()
    {
        var map = TerritoryLoader.FromJson(WallJson);
        var source = new Country(A, "Alpha", new Vector2(10, 51), 100, 100, Bloc.East, "L1");
        var middle = new Country(C, "Charlie", new Vector2(100, 51), 100, 100, Bloc.NonAligned, "L1");
        var target = new Country(B, "Bravo", new Vector2(190, 51), 100, 100, Bloc.West, "L1");
        return new Setup(new RouteNetwork(map, Array.Empty<Port>()), source, target, new[] { source, middle, target });
    }

    private static bool Peaceful(CountryId id) => false;

    private static Func<CountryId, bool> HostileTo(WorldState state, Country source, Country target) =>
        id => state.Relations.Get(source.Id, id) < RouteNetwork.HostileThreshold
            || state.Relations.Get(target.Id, id) < RouteNetwork.HostileThreshold;

    [Fact]
    public void ANavalShipmentRunsTruckThenShipThenTruck()
    {
        var setup = Strait(StraitJson, new Vector2(190, 51));
        var route = setup.Network.Plan(TransportMode.Naval, setup.Source, setup.Target, setup.Countries, Peaceful);
        Assert.NotNull(route);
        Assert.Equal(
            new[] { UnitKind.Truck, UnitKind.CargoShip, UnitKind.Truck },
            route!.Legs.Select(l => l.Carrier).ToArray());
        Assert.True(setup.Network.IsLegal(route, setup.Source, setup.Target, setup.Countries, Peaceful));
    }

    [Fact]
    public void LegsMeetAtTheHandOffPoints()
    {
        var setup = Strait(StraitJson, new Vector2(190, 51));
        var route = setup.Network.Plan(TransportMode.Naval, setup.Source, setup.Target, setup.Countries, Peaceful)!;
        for (var i = 1; i < route.Legs.Count; i++)
        {
            Assert.Equal(route.Legs[i - 1].Points[^1], route.Legs[i].Points[0]);
        }
        Assert.Equal(route.Legs.Count - 1, route.HandOffs.Count);
        Assert.Equal(route.Legs[1].Points[0], route.HandOffs[0]);
    }

    [Fact]
    public void ShipLegsStayOnWater()
    {
        var setup = Strait(StraitJson, new Vector2(190, 51));
        var route = setup.Network.Plan(TransportMode.Naval, setup.Source, setup.Target, setup.Countries, Peaceful)!;
        foreach (var leg in route.Legs)
        {
            for (var i = 1; i < leg.Points.Count; i++)
            {
                for (var s = 0; s <= 40; s++)
                {
                    var point = Vector2.Lerp(leg.Points[i - 1], leg.Points[i], s / 40f);
                    if (leg.Carrier == UnitKind.CargoShip)
                    {
                        Assert.True(setup.Network.IsSeaCell(point), $"ship at {point}");
                    }
                }
            }
        }
        Assert.True(route.Legs.Where(l => l.Carrier == UnitKind.CargoShip).All(l => l.Length > 60));
    }

    [Fact]
    public void ACapitalWithNoLandLinkToItsPortIsReachedByHelicopter()
    {
        var setup = Strait(IslandJson, new Vector2(100, 21));
        var route = setup.Network.Plan(TransportMode.Naval, setup.Source, setup.Target, setup.Countries, Peaceful);
        Assert.NotNull(route);
        Assert.Equal(
            new[] { UnitKind.Truck, UnitKind.CargoShip, UnitKind.Helicopter },
            route!.Legs.Select(l => l.Carrier).ToArray());
        Assert.True(setup.Network.IsLegal(route, setup.Source, setup.Target, setup.Countries, Peaceful));
    }

    [Fact]
    public void CarrierAndPositionFollowTheLegsAsProgressAdvances()
    {
        var setup = Strait(StraitJson, new Vector2(190, 51));
        var route = setup.Network.Plan(TransportMode.Naval, setup.Source, setup.Target, setup.Countries, Peaceful)!;
        Assert.Equal(route.Legs[0].Carrier, route.At(0).Carrier);
        Assert.Equal(route.Legs[^1].Carrier, route.At(1).Carrier);
        Assert.Equal(route.Start, route.At(0).Point);
        Assert.True(Vector2.Distance(route.End, route.At(1).Point) < 1e-3f);
        for (var i = 0; i < route.Legs.Count; i++)
        {
            var middle = (route.Legs[i].StartFraction + route.Legs[i].EndFraction) / 2;
            var position = route.At(middle);
            Assert.Equal(i, position.LegIndex);
            Assert.Equal(route.Legs[i].Carrier, position.Carrier);
        }
        var previous = -1;
        for (var step = 0; step <= 100; step++)
        {
            var index = route.LegIndexAt(step / 100.0);
            Assert.True(index >= previous);
            previous = index;
        }
        Assert.Equal(0.0, route.Legs[0].StartFraction);
        Assert.Equal(1.0, route.Legs[^1].EndFraction);
    }

    [Fact]
    public void AircraftAndTrucksBothCrossOnlyWhatTheyMay()
    {
        var wall = Wall();
        Assert.Null(wall.Network.Plan(TransportMode.Ground, wall.Source, wall.Target, wall.Countries, id => id == C));
        Assert.Null(wall.Network.Plan(TransportMode.Air, wall.Source, wall.Target, wall.Countries, id => id == C));
    }

    [Fact]
    public void ACrossingThroughAPeacefulCountryCountsAsExposureAndIsRefusedOnceItTurnsHostile()
    {
        var wall = Wall();
        var route = wall.Network.Plan(TransportMode.Ground, wall.Source, wall.Target, wall.Countries, Peaceful);
        Assert.NotNull(route);
        Assert.True(route!.Exposure > 0);
        Assert.Single(route.Legs);
        Assert.Equal(UnitKind.Truck, route.Legs[0].Carrier);
        Assert.True(wall.Network.IsLegal(route, wall.Source, wall.Target, wall.Countries, Peaceful));
        Assert.False(wall.Network.IsLegal(route, wall.Source, wall.Target, wall.Countries, id => id == C));
    }

    private static double LengthInside(TerritoryMap map, RouteLeg leg, CountryId country)
    {
        var total = 0.0;
        for (var i = 1; i < leg.Points.Count; i++)
        {
            map.WalkSegment(leg.Points[i - 1], leg.Points[i], (cell, enter, leave) =>
            {
                if (map.OwnerAt(cell) == country)
                {
                    total += Vector2.Distance(enter, leave);
                }
                return true;
            });
        }
        return total;
    }

    [Fact]
    public void SmoothingDoesNotCutAcrossACountryThePathWentAround()
    {
        const string notch =
            "{\"width\":200,\"height\":100,\"entities\":["
            + "{\"id\":\"AAA\",\"rings\":[[0,0,140,0,140,40,60,40,60,100,0,100]]},"
            + "{\"id\":\"CCC\",\"rings\":[[60,40,140,40,140,100,60,100]]},"
            + "{\"id\":\"BBB\",\"rings\":[[140,0,200,0,200,100,140,100]]}"
            + "]}";
        var map = TerritoryLoader.FromJson(notch);
        var source = new Country(A, "Alpha", new Vector2(11, 81), 100, 100, Bloc.East, "L1");
        var middle = new Country(C, "Charlie", new Vector2(101, 81), 100, 100, Bloc.NonAligned, "L1");
        var target = new Country(B, "Bravo", new Vector2(189, 81), 100, 100, Bloc.West, "L1");
        var network = new RouteNetwork(map, Array.Empty<Port>());
        var route = network.Plan(TransportMode.Ground, source, target, new[] { source, middle, target }, Peaceful);
        Assert.NotNull(route);
        Assert.Equal(0, route!.Exposure);
        Assert.True(LengthInside(map, route.Legs[0], C) < 0.1, "the straightened line entered Charlie");
    }

    [Theory]
    [InlineData("PRK")]
    [InlineData("VNM")]
    [InlineData("PRC")]
    public void TheSovietRunToItsPacificPortStaysOutOfChina(string receiver)
    {
        var state = DefaultScenario.Build(42).State;
        var source = state.Countries.Get(new CountryId("USSR"));
        var target = state.Countries.Get(new CountryId(receiver));
        var route = state.Routes!.Plan(TransportMode.Naval, source, target, state.Countries.All, Peaceful);
        Assert.NotNull(route);
        Assert.Equal(UnitKind.Truck, route!.Legs[0].Carrier);
        Assert.Equal(UnitKind.CargoShip, route.Legs[1].Carrier);
        Assert.Equal(route.Legs[0].Points[^1], route.Legs[1].Points[0]);
        var inChina = LengthInside(state.Territory!, route.Legs[0], new CountryId("PRC"));
        Assert.True(inChina < 0.1, $"{inChina:0.0} units of the truck run are inside China");
    }

    [Fact]
    public void ACountryWithoutAPortCannotShipByShip()
    {
        var setup = Strait(StraitJson, new Vector2(190, 51), withPorts: false);
        Assert.Null(setup.Network.Plan(TransportMode.Naval, setup.Source, setup.Target, setup.Countries, Peaceful));
        var best = setup.Network.PlanBest(TransportMode.Naval, setup.Source, setup.Target, setup.Countries, Peaceful);
        Assert.NotNull(best);
        Assert.NotEqual(TransportMode.Naval, best!.Value.Mode);
    }

    [Fact]
    public void PlanningIsDeterministicAcrossFreshNetworks()
    {
        var first = Strait(IslandJson, new Vector2(100, 21));
        var second = Strait(IslandJson, new Vector2(100, 21));
        var one = first.Network.Plan(TransportMode.Naval, first.Source, first.Target, first.Countries, Peaceful)!;
        var two = second.Network.Plan(TransportMode.Naval, second.Source, second.Target, second.Countries, Peaceful)!;
        Assert.Equal(one.Legs.Select(l => l.Carrier), two.Legs.Select(l => l.Carrier));
        Assert.Equal(
            one.Legs.SelectMany(l => l.Points).ToList(),
            two.Legs.SelectMany(l => l.Points).ToList());
    }

    [Fact]
    public void PortsSitOnWaterBesideTheirOwnersLand()
    {
        var state = DefaultScenario.Build(42).State;
        var network = state.Routes!;
        Assert.NotEmpty(network.Ports);
        foreach (var port in network.Ports)
        {
            Assert.True(network.IsSeaCell(port.Berth), $"{port.Name} berth {port.Berth} is not water");
            Assert.NotNull(state.Territory!.FindNearest(Domain.Land, port.Owner, port.Berth, 3f));
        }
    }

    [Fact]
    public void TheSovietUnionShipsFromFourSeas()
    {
        var network = DefaultScenario.Build(42).State.Routes!;
        var soviet = network.Ports.Where(p => p.Owner.Value == "USSR").ToList();
        Assert.Equal(4, soviet.Count);
        Assert.Equal(4, soviet.Select(p => p.Berth).Distinct().Count());
        foreach (var name in new[] { "Murmansk", "Baltiysk", "Sevastopol", "Vladivostok" })
        {
            Assert.Contains(soviet, p => p.Name == name);
        }
    }

    [Theory]
    [InlineData("Sevastopol", "Naples")]
    [InlineData("Port Said", "Mumbai")]
    [InlineData("Norfolk", "San Diego")]
    [InlineData("Murmansk", "Norfolk")]
    [InlineData("Baltiysk", "Norfolk")]
    [InlineData("Vladivostok", "San Diego")]
    public void SeaLanesConnectTheOceansThroughTheChokepoints(string from, string to)
    {
        var network = DefaultScenario.Build(42).State.Routes!;
        var start = network.Ports.Single(p => p.Name == from);
        var end = network.Ports.Single(p => p.Name == to);
        var distance = network.SeaDistance(start.Berth, end.Berth);
        Assert.True(distance is not null, $"no sea path from {from} to {to}");
        Assert.True(distance >= Vector2.Distance(start.Berth, end.Berth) - 1e-3);
    }

    [Fact]
    public void ACountryWithoutPortsHasNoNavalRoute()
    {
        var state = DefaultScenario.Build(42).State;
        var czech = state.Countries.Get(new CountryId("CZE"));
        var poland = state.Countries.Get(new CountryId("POL"));
        Assert.Null(state.Routes!.Plan(TransportMode.Naval, czech, poland, state.Countries.All, Peaceful));
    }

    [Fact]
    public void EveryPlannedRouteOnTheRealMapIsLegal()
    {
        var state = DefaultScenario.Build(42).State;
        var network = state.Routes!;
        var countries = state.Countries.All;
        var planned = 0;
        var ships = 0;
        foreach (var sourceId in new[] { "USSR", "USA", "PRC", "CUB" })
        {
            var source = countries.Single(c => c.Id.Value == sourceId);
            foreach (var target in countries.Where(c => c.Id != source.Id))
            {
                var hostile = HostileTo(state, source, target);
                foreach (var mode in new[] { TransportMode.Naval, TransportMode.Air, TransportMode.Ground })
                {
                    if (mode == TransportMode.Ground && source.Landmass != target.Landmass)
                    {
                        continue;
                    }
                    var route = network.Plan(mode, source, target, countries, hostile);
                    if (route is null)
                    {
                        continue;
                    }
                    planned++;
                    ships += route.Legs.Count(l => l.Carrier == UnitKind.CargoShip);
                    Assert.True(
                        network.IsLegal(route, source, target, countries, hostile),
                        $"{mode} route {source.Id.Value} to {target.Id.Value} is not legal");
                    for (var i = 1; i < route.Legs.Count; i++)
                    {
                        Assert.Equal(route.Legs[i - 1].Points[^1], route.Legs[i].Points[0]);
                    }
                }
            }
        }
        Assert.True(planned >= 60, $"only {planned} routes were planned");
        Assert.True(ships >= 20, $"only {ships} ship legs were planned");
    }

    [Fact]
    public void ShipmentsInARunningWorldCarryLegalRoutes()
    {
        var simulation = DefaultScenario.Build(42);
        var state = simulation.State;
        var network = state.Routes!;
        var seen = new Dictionary<int, Interaction>();
        for (var step = 0; step < 60; step++)
        {
            simulation.Run(25);
            foreach (var item in state.Interactions.All.Where(i => i.Kind == InteractionKind.SupplyShipment))
            {
                seen[item.Id] = item;
            }
        }
        Assert.NotEmpty(seen);
        foreach (var item in seen.Values)
        {
            Assert.NotNull(item.Route);
            var source = state.Countries.Get(item.Source);
            var target = state.Countries.Get(item.Target);
            Assert.True(network.IsLegal(item.Route!, source, target, state.Countries.All, Peaceful), $"shipment {item.Id}");
            Assert.True(item.DueTick > item.StartTick);
        }
    }

    [Fact]
    public void BareWorldsKeepDirectShipments()
    {
        var world = new WorldState(5, 2000, 1000);
        world.Countries.Add(new Country(A, "Alpha", new Vector2(200, 300), 600, 500, Bloc.East, "L1"));
        world.Countries.Add(new Country(B, "Bravo", new Vector2(900, 400), 600, 500, Bloc.West, "L2"));
        world.Countries.Add(new Country(C, "Charlie", new Vector2(500, 700), 600, 500, Bloc.NonAligned, "L1"));
        world.Relations.Set(A, B, 0.6);
        world.Relations.Set(A, C, 0.6);
        world.Relations.Set(B, C, 0.4);
        var simulation = new WorldSimulation(world, null, null, new InteractionEngine(null, spawnChance: 1.0));
        var shipments = new Dictionary<int, Interaction>();
        for (var step = 0; step < 100; step++)
        {
            simulation.Run(3);
            foreach (var item in world.Interactions.All.Where(i => i.Kind == InteractionKind.SupplyShipment))
            {
                shipments[item.Id] = item;
            }
        }
        Assert.NotEmpty(shipments);
        Assert.All(shipments.Values, i => Assert.Null(i.Route));
    }

    [Fact]
    public void ProgressRunsFromStartToDueAndFreezesWhenTheShipmentEnds()
    {
        var interaction = new Interaction(
            1, InteractionKind.SupplyShipment, A, B, TransportMode.Ground, 10, "supplies", 100, 200, 150,
            InteractionStatus.Succeeded);
        Assert.Equal(0.0, interaction.ProgressAt(100));
        Assert.Equal(0.5, interaction.ProgressAt(150));
        Assert.Equal(1.0, interaction.ProgressAt(300));
        Assert.Equal(0.0, interaction.ProgressAt(50));
        interaction.Complete(150);
        Assert.Equal(0.5, interaction.ProgressAt(999));
    }

    [Fact]
    public void SnapshotShipmentsCarryTheirRouteAndTheCarrierOfTheirCurrentLeg()
    {
        var simulation = DefaultScenario.Build(42);
        var seen = 0;
        var multiLeg = 0;
        for (var step = 0; step < 60; step++)
        {
            simulation.Run(25);
            var snapshot = GroundTruthMapper.Map(simulation);
            foreach (var item in snapshot.Interactions)
            {
                if (item.Kind != "SupplyShipment")
                {
                    Assert.Null(item.Route);
                    Assert.Null(item.Carrier);
                    continue;
                }
                Assert.NotNull(item.Route);
                var legs = item.Route!.Legs;
                Assert.NotEmpty(legs);
                Assert.Equal(0.0, legs[0].Start);
                Assert.Equal(1.0, legs[^1].End);
                for (var i = 0; i < legs.Count; i++)
                {
                    Assert.True(legs[i].Points.Count >= 4 && legs[i].Points.Count % 2 == 0);
                    if (i > 0)
                    {
                        Assert.Equal(legs[i - 1].End, legs[i].Start);
                    }
                }
                var reference = item.EndTick ?? snapshot.Tick;
                var progress = Math.Clamp((reference - item.StartTick) / (double)Math.Max(1, item.DueTick - item.StartTick), 0.0, 1.0);
                var current = legs.FirstOrDefault(l => progress <= l.End) ?? legs[^1];
                Assert.Equal(current.Carrier, item.Carrier);
                seen++;
                if (legs.Count > 1)
                {
                    multiLeg++;
                }
            }
        }
        Assert.True(seen > 0);
        Assert.True(multiLeg > 0, "no multi-leg shipment was seen");
    }
}
