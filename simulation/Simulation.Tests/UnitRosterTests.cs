using System.Numerics;
using Simulation.API.Scenarios;
using Simulation.API.Services;
using WorldEngine.Countries;
using WorldEngine.Sensors;
using WorldEngine.Units;
using WorldEngine.World;
using Xunit;

namespace Simulation.Tests;

public class UnitRosterTests
{
    private static readonly CountryId A = new("AAA");
    private static readonly CountryId B = new("BBB");

    private static readonly string[] Superpowers = { "USSR", "USA" };
    private static readonly string[] RegionalPowers = { "PRC", "GBR", "FRA", "FRG", "IND", "JPN", "ITA", "TUR", "KOR" };
    private static readonly string[] SmallStates = { "HUN", "ROU", "BGR", "CUB" };

    private static readonly UnitKind[] Legacy =
    {
        UnitKind.Armor, UnitKind.Naval, UnitKind.Air, UnitKind.SupplyConvoy
    };

    private static WorldState TwoCountryWorld(double supplies = 0, double ammunition = 0)
    {
        var world = new WorldState(1, 100, 100);
        world.Countries.Add(new Country(A, "A", new Vector2(10, 10), supplies, ammunition));
        world.Countries.Add(new Country(B, "B", new Vector2(80, 80), supplies, ammunition));
        return world;
    }

    private static Dictionary<string, double> PowerByCountry(int seed)
    {
        var state = DefaultScenario.Build(seed).State;
        return state.Countries.All.ToDictionary(c => c.Id.Value, c => NationalPower.Of(state, c.Id).Total);
    }

    [Fact]
    public void EveryUnitKindHasASpec()
    {
        foreach (var kind in Enum.GetValues<UnitKind>())
        {
            var spec = UnitCatalog.Of(kind);
            Assert.Equal(kind, spec.Kind);
            Assert.True(spec.Speed > 0, $"{kind} speed");
            Assert.True(spec.Strength > 0, $"{kind} strength");
            Assert.True(spec.Range > 0, $"{kind} range");
            Assert.True(spec.PowerWeight > 0, $"{kind} weight");
        }
    }

    [Theory]
    [InlineData(UnitKind.Tank, UnitRole.Combat)]
    [InlineData(UnitKind.ArmoredCar, UnitRole.Combat)]
    [InlineData(UnitKind.Warship, UnitRole.Combat)]
    [InlineData(UnitKind.Warplane, UnitRole.Combat)]
    [InlineData(UnitKind.Helicopter, UnitRole.Combat)]
    [InlineData(UnitKind.Infantry, UnitRole.Combat)]
    [InlineData(UnitKind.Artillery, UnitRole.Combat)]
    [InlineData(UnitKind.Truck, UnitRole.Carrier)]
    [InlineData(UnitKind.CargoShip, UnitRole.Carrier)]
    [InlineData(UnitKind.CargoPlane, UnitRole.Carrier)]
    [InlineData(UnitKind.SupplyConvoy, UnitRole.Carrier)]
    public void CombatUnitsAndCarriersAreSeparateRoles(UnitKind kind, UnitRole expected)
    {
        Assert.Equal(expected, kind.RoleOf());
    }

    [Fact]
    public void CarriersAreWorthLessInPowerThanAnyCombatUnitOfTheirDomain()
    {
        foreach (var carrier in UnitCatalog.All.Where(s => s.Role == UnitRole.Carrier))
        {
            foreach (var fighter in UnitCatalog.All.Where(s => s.Role == UnitRole.Combat && s.Domain == carrier.Domain))
            {
                Assert.True(carrier.PowerWeight < fighter.PowerWeight, $"{carrier.Kind} vs {fighter.Kind}");
            }
        }
    }

    [Fact]
    public void AddUnitFromTheCatalogUsesTheSpecFigures()
    {
        var world = TwoCountryWorld();
        var tank = world.AddUnit(A, UnitKind.Tank, new Vector2(10, 10));
        var spec = UnitCatalog.Of(UnitKind.Tank);
        Assert.Equal(spec.Speed, tank.Speed);
        Assert.Equal(spec.Strength, tank.Strength);
    }

    [Fact]
    public void PowerAddsFieldedForcesAndStockpiles()
    {
        var world = TwoCountryWorld(supplies: 100, ammunition: 100);
        world.AddUnit(A, UnitKind.Tank, new Vector2(10, 10));
        var power = NationalPower.Of(world, A);
        Assert.Equal(UnitCatalog.Of(UnitKind.Tank).PowerWeight, power.Forces, 6);
        Assert.Equal(
            100 * NationalPower.SuppliesWeight + 100 * NationalPower.AmmunitionWeight,
            power.Stockpiles,
            6);
        Assert.Equal(power.Forces + power.Stockpiles, power.Total, 6);
        Assert.Equal(power.Stockpiles, NationalPower.Of(world, B).Total, 6);
    }

    [Fact]
    public void DamageLowersPowerAndDestructionRemovesTheUnit()
    {
        var world = TwoCountryWorld();
        var tank = world.AddUnit(A, UnitKind.Tank, new Vector2(10, 10));
        var full = NationalPower.Of(world, A).Total;
        tank.ApplyDamage(50);
        var damaged = NationalPower.Of(world, A).Total;
        Assert.True(damaged < full);
        Assert.Equal(full / 2, damaged, 6);
        tank.ApplyDamage(1000);
        Assert.Equal(0, NationalPower.Of(world, A).Total, 6);
    }

    [Fact]
    public void PowerFollowsTheStockpiles()
    {
        var world = TwoCountryWorld(supplies: 100, ammunition: 100);
        var before = NationalPower.Of(world, A).Total;
        world.Countries.Get(A).AddAmmunition(-100);
        Assert.True(NationalPower.Of(world, A).Total < before);
    }

    [Theory]
    [InlineData(42)]
    [InlineData(7)]
    public void SuperpowersOutrankRegionalPowersAndRegionalPowersOutrankSmallStates(int seed)
    {
        var power = PowerByCountry(seed);
        var superMin = Superpowers.Min(id => power[id]);
        var regionalMax = RegionalPowers.Max(id => power[id]);
        var regionalMin = RegionalPowers.Min(id => power[id]);
        var smallMax = SmallStates.Max(id => power[id]);
        Assert.True(superMin > regionalMax, $"superpower {superMin} vs regional {regionalMax}");
        Assert.True(regionalMin > smallMax, $"regional {regionalMin} vs small {smallMax}");
    }

    [Fact]
    public void ScenarioFieldsTheSpecificKindsAndNoLegacyOnes()
    {
        var state = DefaultScenario.Build(42).State;
        var kinds = state.Units.Select(u => u.Kind).ToHashSet();
        foreach (var legacy in Legacy)
        {
            Assert.DoesNotContain(legacy, kinds);
        }
        foreach (var kind in new[]
        {
            UnitKind.Infantry, UnitKind.Artillery, UnitKind.Tank, UnitKind.ArmoredCar, UnitKind.Warship,
            UnitKind.Warplane, UnitKind.Helicopter, UnitKind.Truck, UnitKind.CargoShip, UnitKind.CargoPlane
        })
        {
            Assert.Contains(kind, kinds);
        }
    }

    [Fact]
    public void EveryUnitStartsWithItsCatalogFigures()
    {
        var state = DefaultScenario.Build(42).State;
        Assert.All(state.Units, u =>
        {
            var spec = UnitCatalog.Of(u.Kind);
            Assert.Equal(spec.Speed, u.Speed);
            Assert.Equal(spec.Strength, u.Strength);
        });
    }

    [Fact]
    public void SuperpowersFieldCarriersAlongsideTheirCombatUnits()
    {
        var state = DefaultScenario.Build(42).State;
        foreach (var id in Superpowers)
        {
            var owned = state.Units.Where(u => u.Owner.Value == id).ToList();
            Assert.Contains(owned, u => u.Kind.RoleOf() == UnitRole.Combat);
            Assert.Contains(owned, u => u.Kind.RoleOf() == UnitRole.Carrier);
            Assert.True(owned.Count(u => u.Kind.RoleOf() == UnitRole.Combat) > owned.Count(u => u.Kind.RoleOf() == UnitRole.Carrier));
        }
    }

    [Fact]
    public void SnapshotCarriesComputedPowerForEveryCountry()
    {
        var simulation = DefaultScenario.Build(42);
        var snapshot = GroundTruthMapper.Map(simulation);
        Assert.All(snapshot.Countries, c => Assert.True(c.Power > 0, $"{c.Id} has no power"));
        var ussr = snapshot.Countries.Single(c => c.Id == "USSR");
        var expected = NationalPower.Of(simulation.State, new CountryId("USSR")).Total;
        Assert.Equal(Math.Round(expected, 1), ussr.Power);
    }

    [Fact]
    public void PowerChangesWhenTheWorldDoes()
    {
        var simulation = DefaultScenario.Build(42);
        var before = PowerByCountry(42);
        simulation.Run(1500);
        var state = simulation.State;
        var after = state.Countries.All.ToDictionary(c => c.Id.Value, c => NationalPower.Of(state, c.Id).Total);
        Assert.Contains(before.Keys, id => Math.Abs(before[id] - after[id]) > 0.01);
    }

    private static int Detections(Sensor sensor, UnitKind kind)
    {
        var world = TwoCountryWorld();
        world.AddUnit(B, kind, new Vector2(12, 10));
        var rng = new Random(3);
        var count = 0;
        for (var i = 0; i < 200; i++)
        {
            count += sensor.Sense(world, rng).Count;
        }
        return count;
    }

    [Theory]
    [InlineData(UnitKind.Tank)]
    [InlineData(UnitKind.ArmoredCar)]
    [InlineData(UnitKind.Warship)]
    [InlineData(UnitKind.Warplane)]
    [InlineData(UnitKind.Helicopter)]
    [InlineData(UnitKind.Truck)]
    [InlineData(UnitKind.CargoShip)]
    [InlineData(UnitKind.CargoPlane)]
    public void RadarSeesEveryVehicleShipAndAircraft(UnitKind kind)
    {
        Assert.True(Detections(SensorProfiles.Radar(1, A, new Vector2(10, 10)), kind) > 0);
    }

    [Theory]
    [InlineData(UnitKind.Infantry)]
    [InlineData(UnitKind.Artillery)]
    public void RadarDoesNotSeeMenOnFootOrGuns(UnitKind kind)
    {
        Assert.Equal(0, Detections(SensorProfiles.Radar(1, A, new Vector2(10, 10)), kind));
    }

    [Theory]
    [InlineData(UnitKind.Warship, true)]
    [InlineData(UnitKind.CargoShip, true)]
    [InlineData(UnitKind.Tank, false)]
    [InlineData(UnitKind.Warplane, false)]
    [InlineData(UnitKind.Truck, false)]
    public void SonarSeesShipsOnly(UnitKind kind, bool seen)
    {
        var count = Detections(SensorProfiles.Sonar(2, A, new Vector2(10, 10)), kind);
        Assert.Equal(seen, count > 0);
    }
}
