using System.Numerics;
using WorldEngine.Countries;
using WorldEngine.Units;

namespace WorldEngine.Sensors;

public static class SensorProfiles
{
    public static Sensor Radar(int id, CountryId owner, Vector2 position) =>
        new(id, SensorKind.Radar, owner, position, 400, 0.9, 8, 1, 0.6,
            new[] { UnitKind.Air, UnitKind.Naval, UnitKind.Armor, UnitKind.SupplyConvoy });

    public static Sensor Sonar(int id, CountryId owner, Vector2 position) =>
        new(id, SensorKind.Sonar, owner, position, 250, 0.8, 15, 2, 0.5,
            new[] { UnitKind.Naval });

    public static Sensor Optical(int id, CountryId owner, Vector2 position) =>
        new(id, SensorKind.Optical, owner, position, 120, 0.95, 2, 0, 0.95,
            Enum.GetValues<UnitKind>());
}
