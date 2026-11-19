using System.Numerics;
using WorldEngine.Countries;
using WorldEngine.Sensors;
using WorldEngine.Units;
using WorldEngine.World;

namespace Simulation.API.Scenarios;

public static class DefaultScenario
{
    public static WorldSimulation Build(int seed)
    {
        var usa = new CountryId("USA");
        var ita = new CountryId("ITA");
        var jpn = new CountryId("JPN");

        var world = new WorldState(seed, 1000, 1000);
        world.Countries.Add(new Country(usa, "United States", new Vector2(150, 200), 500, 500));
        world.Countries.Add(new Country(ita, "Italy", new Vector2(800, 200), 300, 300));
        world.Countries.Add(new Country(jpn, "Japan", new Vector2(850, 800), 400, 400));

        world.Relations.Set(usa, ita, 0.2);
        world.Relations.Set(usa, jpn, 0.5);
        world.Relations.Set(ita, jpn, 0.1);

        world.AddUnit(usa, UnitKind.Artillery, new Vector2(160, 210), 0.03, 100);
        world.AddUnit(usa, UnitKind.Armor, new Vector2(220, 260), 0.08, 100);
        world.AddUnit(usa, UnitKind.SupplyConvoy, new Vector2(140, 300), 0.06, 60);
        world.AddUnit(ita, UnitKind.Armor, new Vector2(760, 240), 0.08, 100);
        world.AddUnit(ita, UnitKind.Naval, new Vector2(700, 120), 0.12, 120);
        world.AddUnit(ita, UnitKind.Air, new Vector2(820, 180), 0.4, 80);
        world.AddUnit(jpn, UnitKind.Naval, new Vector2(900, 700), 0.12, 120);
        world.AddUnit(jpn, UnitKind.SupplyConvoy, new Vector2(820, 760), 0.06, 60);
        world.AddUnit(jpn, UnitKind.Infantry, new Vector2(860, 830), 0.04, 100);

        var sensors = new SensorNetwork(seed + 1000);
        sensors.Add(SensorProfiles.Radar(1, usa, new Vector2(150, 200)));
        sensors.Add(SensorProfiles.Optical(2, usa, new Vector2(150, 200)));
        sensors.Add(SensorProfiles.Sonar(3, usa, new Vector2(120, 260)));

        return new WorldSimulation(world, null, sensors);
    }
}
