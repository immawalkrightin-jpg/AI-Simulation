using System.Numerics;
using WorldEngine.Countries;
using WorldEngine.Units;

namespace WorldEngine.Sensors;

public static class SensorProfiles
{
    // Radar sees vehicles, ships and aircraft, not men on foot or guns in the open.
    private static readonly UnitKind[] RadarKinds =
    {
        UnitKind.Air, UnitKind.Naval, UnitKind.Armor, UnitKind.SupplyConvoy,
        UnitKind.Tank, UnitKind.ArmoredCar, UnitKind.Warship, UnitKind.Warplane, UnitKind.Helicopter,
        UnitKind.Truck, UnitKind.CargoShip, UnitKind.CargoPlane
    };

    private static readonly UnitKind[] SonarKinds = { UnitKind.Naval, UnitKind.Warship, UnitKind.CargoShip };

    public static Sensor Radar(int id, CountryId owner, Vector2 position) =>
        new(id, SensorKind.Radar, owner, position, 400, 0.9, 8, 1, 0.6, RadarKinds);

    public static Sensor Sonar(int id, CountryId owner, Vector2 position) =>
        new(id, SensorKind.Sonar, owner, position, 250, 0.8, 15, 2, 0.5, SonarKinds);

    public static Sensor Optical(int id, CountryId owner, Vector2 position) =>
        new(id, SensorKind.Optical, owner, position, 120, 0.95, 2, 0, 0.95,
            Enum.GetValues<UnitKind>());
}
