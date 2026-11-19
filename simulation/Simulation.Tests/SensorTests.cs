using System.Numerics;
using System.Reflection;
using WorldEngine.Countries;
using WorldEngine.Sensors;
using WorldEngine.Units;
using WorldEngine.World;
using Xunit;

namespace Simulation.Tests;

public class SensorTests
{
    private static readonly CountryId Usa = new("USA");
    private static readonly CountryId Ita = new("ITA");
    private static readonly CountryId Jpn = new("JPN");

    private static readonly UnitKind[] AllKinds = Enum.GetValues<UnitKind>();

    private static WorldState TwoCountryWorld()
    {
        var world = new WorldState(1, 1000, 1000);
        world.Countries.Add(new Country(Usa, "United States", new Vector2(0, 0), 100, 100));
        world.Countries.Add(new Country(Ita, "Italy", new Vector2(900, 900), 100, 100));
        return world;
    }

    private static WorldState ThreeCountryWorld(int seed)
    {
        var world = new WorldState(seed, 1000, 1000);
        world.Countries.Add(new Country(Usa, "United States", new Vector2(100, 100), 500, 500));
        world.Countries.Add(new Country(Ita, "Italy", new Vector2(800, 200), 300, 300));
        world.Countries.Add(new Country(Jpn, "Japan", new Vector2(900, 800), 400, 400));
        world.AddUnit(Usa, UnitKind.Armor, new Vector2(100, 100), 5, 100);
        world.AddUnit(Ita, UnitKind.Naval, new Vector2(800, 200), 8, 100);
        world.AddUnit(Jpn, UnitKind.SupplyConvoy, new Vector2(900, 800), 6, 50);
        return world;
    }

    private static Sensor TestSensor(SensorKind kind, float range, float noise, int delay, IEnumerable<UnitKind> detects) =>
        new(1, kind, Usa, Vector2.Zero, range, 1.0, noise, delay, 1.0, detects);

    private static int CountObservations(WorldState world, Sensor sensor, int attempts)
    {
        var network = new SensorNetwork(5);
        network.Add(sensor);
        for (var i = 0; i < attempts; i++)
        {
            network.Sense(world);
        }
        return network.Collect(long.MaxValue).Count;
    }

    [Fact]
    public void DetectsEnemyInRange()
    {
        var world = TwoCountryWorld();
        world.AddUnit(Ita, UnitKind.Armor, new Vector2(10, 0), 1, 100);
        Assert.True(CountObservations(world, TestSensor(SensorKind.Radar, 100, 0, 0, AllKinds), 50) > 0);
    }

    [Fact]
    public void IgnoresEnemyOutOfRange()
    {
        var world = TwoCountryWorld();
        world.AddUnit(Ita, UnitKind.Armor, new Vector2(500, 0), 1, 100);
        Assert.Equal(0, CountObservations(world, TestSensor(SensorKind.Radar, 100, 0, 0, AllKinds), 50));
    }

    [Fact]
    public void IgnoresOwnUnits()
    {
        var world = TwoCountryWorld();
        world.AddUnit(Usa, UnitKind.Armor, new Vector2(10, 0), 1, 100);
        Assert.Equal(0, CountObservations(world, TestSensor(SensorKind.Radar, 100, 0, 0, AllKinds), 50));
    }

    [Fact]
    public void IgnoresDestroyedUnits()
    {
        var world = TwoCountryWorld();
        world.AddUnit(Ita, UnitKind.Armor, new Vector2(10, 0), 1, 100).ApplyDamage(1000);
        Assert.Equal(0, CountObservations(world, TestSensor(SensorKind.Radar, 100, 0, 0, AllKinds), 50));
    }

    [Fact]
    public void SonarIgnoresNonNavalUnits()
    {
        var world = TwoCountryWorld();
        world.AddUnit(Ita, UnitKind.Armor, new Vector2(10, 0), 1, 100);
        var sensor = TestSensor(SensorKind.Sonar, 100, 0, 0, new[] { UnitKind.Naval });
        Assert.Equal(0, CountObservations(world, sensor, 50));
    }

    [Fact]
    public void ObservationsAreHeldUntilDeliveryTick()
    {
        var world = TwoCountryWorld();
        world.AddUnit(Ita, UnitKind.Armor, new Vector2(10, 0), 1, 100);
        var network = new SensorNetwork(5);
        network.Add(TestSensor(SensorKind.Radar, 100, 0, 3, AllKinds));
        for (var i = 0; i < 20; i++)
        {
            network.Sense(world);
        }
        Assert.Empty(network.Collect(2));
        var delivered = network.Collect(3);
        Assert.NotEmpty(delivered);
        Assert.All(delivered, o => Assert.Equal(0, o.ObservedTick));
        Assert.All(delivered, o => Assert.Equal(3, o.DeliveryTick));
    }

    [Fact]
    public void ObservedPositionIsNoisyAndCarriesUncertainty()
    {
        var world = TwoCountryWorld();
        var truth = new Vector2(10, 0);
        world.AddUnit(Ita, UnitKind.Armor, truth, 1, 100);
        var network = new SensorNetwork(5);
        network.Add(TestSensor(SensorKind.Radar, 100, 5, 0, AllKinds));
        for (var i = 0; i < 30; i++)
        {
            network.Sense(world);
        }
        var observations = network.Collect(long.MaxValue);
        Assert.NotEmpty(observations);
        Assert.Contains(observations, o => o.Position != truth);
        Assert.All(observations, o => Assert.True(o.PositionSigma > 0));
    }

    [Fact]
    public void CloseOpticalContactIsConfirmedAndIdentifiesOwner()
    {
        var world = TwoCountryWorld();
        world.AddUnit(Ita, UnitKind.Armor, new Vector2(10, 0), 1, 100);
        var network = new SensorNetwork(5);
        network.Add(TestSensor(SensorKind.Optical, 100, 1, 0, AllKinds));
        for (var i = 0; i < 20; i++)
        {
            network.Sense(world);
        }
        var observations = network.Collect(long.MaxValue);
        Assert.NotEmpty(observations);
        Assert.All(observations, o => Assert.Equal(ObservationConfidence.Confirmed, o.Confidence));
        Assert.All(observations, o => Assert.Equal(Ita, o.ClassifiedOwner));
    }

    [Fact]
    public void RadarNeverIdentifiesOwner()
    {
        var world = TwoCountryWorld();
        world.AddUnit(Ita, UnitKind.Armor, new Vector2(10, 0), 1, 100);
        var network = new SensorNetwork(5);
        network.Add(TestSensor(SensorKind.Radar, 100, 1, 0, AllKinds));
        for (var i = 0; i < 20; i++)
        {
            network.Sense(world);
        }
        Assert.All(network.Collect(long.MaxValue), o => Assert.Null(o.ClassifiedOwner));
    }

    [Fact]
    public void ObservationExposesNoGroundTruthIdentity()
    {
        var properties = typeof(Observation).GetProperties();
        Assert.DoesNotContain(properties, p => p.Name == "UnitId" || p.Name == "Id");
        Assert.DoesNotContain(properties, p => p.PropertyType == typeof(Unit) || p.PropertyType == typeof(WorldState));
    }

    [Fact]
    public void ObservationSourceContractDoesNotExposeWorldState()
    {
        var methods = typeof(IObservationSource).GetMethods();
        Assert.All(methods, m =>
        {
            Assert.NotEqual(typeof(WorldState), m.ReturnType);
            Assert.DoesNotContain(m.GetParameters(), p => p.ParameterType == typeof(WorldState));
        });
        Assert.Empty(typeof(IObservationSource).GetProperties(BindingFlags.Public | BindingFlags.Instance));
    }

    [Fact]
    public void SensorsDoNotAlterWorldHistory()
    {
        var plain = new WorldSimulation(ThreeCountryWorld(11));
        var network = new SensorNetwork(99);
        network.Add(SensorProfiles.Radar(1, Usa, new Vector2(500, 500)));
        network.Add(SensorProfiles.Optical(2, Usa, new Vector2(500, 500)));
        var sensed = new WorldSimulation(ThreeCountryWorld(11), null, network);
        plain.Run(200);
        sensed.Run(200);
        Assert.Equal(plain.State.Log.Records, sensed.State.Log.Records);
        Assert.Equal(
            plain.State.Units.Select(u => u.Position).ToList(),
            sensed.State.Units.Select(u => u.Position).ToList());
    }

    [Fact]
    public void RunningSimulationProducesObservations()
    {
        var network = new SensorNetwork(3);
        var sensor = new Sensor(1, SensorKind.Radar, Usa, new Vector2(500, 500), 2000, 0.9, 8, 1, 0.6, AllKinds);
        network.Add(sensor);
        var simulation = new WorldSimulation(ThreeCountryWorld(5), null, network);
        simulation.Run(100);
        Assert.NotEmpty(simulation.Sensors!.Collect(long.MaxValue));
    }

    [Fact]
    public void DuplicateSensorIdIsRejected()
    {
        var network = new SensorNetwork(1);
        network.Add(SensorProfiles.Radar(1, Usa, Vector2.Zero));
        Assert.Throws<InvalidOperationException>(() => network.Add(SensorProfiles.Sonar(1, Usa, Vector2.Zero)));
    }
}
